using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Minecraft_Server_Bot.CraftyControl;

class CraftyControl
{
    public static CraftyControl Instance { get; private set; } = new(CraftyControlConfiguration.Instance);

    public static void OverrideInstance(CraftyControlConfiguration config)
    {
        Instance._httpClient.Dispose();
        Instance._httpClient.Dispose();
        foreach (var server in Instance.MinecraftServers)
            server.DisposeSocket();

        Instance = new CraftyControl(config);
    }

    private static Regex _doneMessageRegex = new(@"\[Server thread\/INFO\]<\/span>(?: \[minecraft\/DedicatedServer\])?: Done \(\d+.\d{3}s\)!", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly Uri _websocketUri;
    private readonly string _webSocketCookie = "";
    public List<MinecraftServer> MinecraftServers { get; private set; } = new();
    public delegate Task ServerStatusChangeEventHandler(object? sender, ServerStateChangeEventArgs e);
    public static event ServerStatusChangeEventHandler ServerStopped;

    private CraftyControl(CraftyControlConfiguration config)
    {
        Uri baseAddress = config.BaseAddress;
        string token = config.Token;

        _httpClient = new()
        {
            BaseAddress = new Uri(baseAddress, "/api/v2/"),
            DefaultRequestHeaders = {
                Authorization = new("Bearer", token)
            }
        };

        MinecraftServer.StateChanged += async (sender, e) =>
        {
            if (e.NewState == MinecraftServerState.Stopped)
                await ServerStopped.Invoke(sender, e);
        };

        // Fetch initial server list for websocket usage
        var serverFetchOp = FetchServersAsync();

        // Get websocket uri
        string wsParams = "page=%2Fpanel%2Fserver_detail&page_query_params=id%3D";
        _websocketUri = new Uri(baseAddress.AbsoluteUri.Replace("https://", "wss://") + "ws?" + wsParams);

        // Get xsrf token and set cookie in websocket client
        do
        {
            try
            {
                var response = _httpClient.GetAsync("/").Result;
                response.EnsureSuccessStatusCode();
                var xsrfToken = response.Headers.GetValues("set-cookie").FirstOrDefault();
                _webSocketCookie = $"token={token}; _xsrf={xsrfToken}";
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.Message);
            }
        } while (_webSocketCookie == "");

        // Wait for server list to finish fetching
        var servers = serverFetchOp.Result;
        _ = UpdateServerStatuses(servers);
    }

    public async Task<List<MinecraftServerWrapper>?> FetchServersAsync()
    {
        try
        {
            HttpResponseMessage serverList = await _httpClient.GetAsync("servers");
            var serversResponse = JsonConvert.DeserializeObject<CraftyControlWrapper>(await serverList.Content.ReadAsStringAsync());
            if (serversResponse == null || serversResponse.Status != "ok")
                throw new HttpRequestException($"Error listing servers: {serversResponse?.Error}");
            var servers = (serversResponse.Data as JArray)!.ToObject<List<MinecraftServerWrapper>>();

            return servers;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    public async Task<MinecraftServerStatusWrapper?> GetServerStatusAsync(int serverId)
    {
        try
        {
            HttpResponseMessage serverList = await _httpClient.GetAsync($"servers/{serverId}/stats");
            var serversResponse = JsonConvert.DeserializeObject<CraftyControlWrapper>(await serverList.Content.ReadAsStringAsync());
            if (serversResponse == null || serversResponse.Status != "ok")
                throw new HttpRequestException($"Error getting server: {serversResponse?.Error}");
            var server = (serversResponse.Data as JObject)!.ToObject<MinecraftServerStatusWrapper>();

            if (server != null)
                _ = UpdateServerStatus(server);

            return server;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    public async Task<bool> StartServer(int serverId)
    {
        // Listen to websocket to detect when the server finished starting
        ClientWebSocket socket = await CreateWebSocketAsync(serverId);

        try
        {
            HttpResponseMessage startServer = await _httpClient.PostAsync($"servers/{serverId}/action/start_server", null);
            var startServerResponse = JsonConvert.DeserializeObject<CraftyControlWrapper>(await startServer.Content.ReadAsStringAsync());
            if (startServerResponse == null || startServerResponse.Status != "ok")
                throw new HttpRequestException($"Error starting server: {startServerResponse?.Error}");
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return false;
        }

        // Send a message and wait for a response
        DateTime startTime = DateTime.Now;
        HttpResponseMessage sendMessage = await _httpClient.PostAsync($"servers/{serverId}/stdin", new StringContent("save-all"));
        sendMessage.EnsureSuccessStatusCode();
        var cancellationSource = new CancellationTokenSource(120000);
        CancellationToken cancellation = cancellationSource.Token;
        while (!cancellation.IsCancellationRequested)
        {
            if (DateTime.Now - startTime > TimeSpan.FromSeconds(15))
            {
                // Poll to see if stopped by error
                var status = await GetServerStatusAsync(serverId);
                if (status?.Running == false)
                {
                    cancellationSource.Cancel();
                    break;
                }
            }

            ArraySegment<byte> buffer = new(new byte[1024]);
            try
            {
                await socket.ReceiveAsync(buffer, cancellation);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (WebSocketException e)
            {
                Console.WriteLine(e.Message);
                cancellationSource.Cancel();
                break;
            }
            if (cancellation.IsCancellationRequested)
                break;
            string recivedMessage = Encoding.UTF8.GetString(buffer).Replace("\0", "");
            var socketEvent = JsonConvert.DeserializeObject<SocketEventWrapper>(recivedMessage);
            if (socketEvent?.Event != "vterm_new_line")
                continue;
            var terminalLine = (socketEvent.Data as JObject)!.ToObject<TerminalLine>();

            if (_doneMessageRegex.IsMatch(terminalLine!.Line!) || terminalLine.Line.Contains("Saved the game"))
                break;
            // else if (terminalLine.Line.Contains("mc-log-error"))
            //     cancellationSource.Cancel();
        }

        MinecraftServer? server = MinecraftServers.FirstOrDefault(s => s.ServerInfo.Id == serverId);
        if (server == null)
        {
            socket.Dispose();
            _ = UpdateServerStatuses();
        }
        else
        {
            server.WebSocket = socket;
        }

        return !cancellation.IsCancellationRequested;
    }

    public async Task<bool> StopServer(int serverId)
    {
        MinecraftServer? server = MinecraftServers.FirstOrDefault(s => s.ServerInfo.Id == serverId);
        if (server != null)
            return await StopServer(server);
        return false;
    }
    public async Task<bool> StopServer(MinecraftServer server)
    {
        try
        {
            HttpResponseMessage stopServer = await _httpClient.PostAsync($"servers/{server.ServerInfo.Id}/action/stop_server", null);
            var stopServerResponse = JsonConvert.DeserializeObject<CraftyControlWrapper>(await stopServer.Content.ReadAsStringAsync());
            if (stopServerResponse == null || stopServerResponse.Status != "ok")
                throw new HttpRequestException($"Error stopping server: {stopServerResponse?.Error}");
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine(e.Message);
            return false;
        }
        
        // Wait for server to finish stopping
        var cancellation = new CancellationTokenSource(120000).Token;
        while (server.Status.Running && !cancellation.IsCancellationRequested)
            await Task.Delay(1000);

        if (cancellation.IsCancellationRequested)
            return false;

        await ServerStopped.Invoke(this, new ServerStateChangeEventArgs(server));
        return true;
    }

    public async Task UpdateServerStatuses(List<MinecraftServerWrapper>? serverList = null)
    {
        while (serverList == null)
            serverList = await FetchServersAsync();

        List<MinecraftServerStatusWrapper> serverStatuses = new();
        foreach (var server in serverList)
        {
            var serverStatus = await GetServerStatusAsync(server.Id);
            if (serverStatus == null)
                continue;
            serverStatuses.Add(serverStatus);
        }

        await UpdateServerList(serverStatuses);
    }
    public async Task UpdateServerList(List<MinecraftServerStatusWrapper> serverList)
    {
        List<Task> updateTasks = new();

        foreach (var server in serverList)
        {
            updateTasks.Add(UpdateServerStatus(server));
        }

        await Task.WhenAll(updateTasks);
    }
    public async Task UpdateServerStatus(MinecraftServerStatusWrapper server)
    {
        MinecraftServer? oldServer = MinecraftServers.Find(s => s.ServerInfo.UUID == server.ServerInfo.UUID);

        if (oldServer == null)
        {
            MinecraftServer newServer = new(server);
            MinecraftServers.Add(newServer);
            return;
        }

        // Update server status
        await oldServer.UpdateStatus(server);
    }

    public async Task<ClientWebSocket> CreateWebSocketAsync(int serverId)
    {
        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Cookie", _webSocketCookie);
        try
        {
            var uri = new Uri(_websocketUri.AbsoluteUri + serverId.ToString());
            await socket.ConnectAsync(uri, CancellationToken.None);
        }
        catch (WebSocketException e)
        {
            Console.WriteLine(e.Message);
        }
        return socket;
    }
}