using System.Net.WebSockets;
using System.Text;
using Minecraft_Server_Bot.Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Minecraft_Server_Bot.CraftyControl;

public class MinecraftServer
{
    public MinecraftServerWrapper ServerInfo => Status.ServerInfo;
    public MinecraftServerStatusWrapper Status { get; private set; }
    public double IdleHours { get; private set; } = 1;

    private WebSocket? _webSocket;
    public WebSocket? WebSocket
    {
        get => _webSocket;
        set
        {
            if (value == null && _webSocket != null)
                _webSocket.Dispose();
            _webSocket = value;

            if (_webSocket != null)
                _ = ListenToWebSocket();
        }
    }
    public MinecraftServerState State { get; private set; }
    public DateTime? LastPlayerTime { get; private set; } = null;

    public delegate void ServerStateChangedHandler(object sender, ServerStateChangeEventArgs e);
    public static event ServerStateChangedHandler StateChanged;

    public MinecraftServer(MinecraftServerStatusWrapper status, WebSocket? webSocket = null)
    {
        Status = status;
        WebSocket = webSocket;
        UpdateState(Status.Running ? MinecraftServerState.Running : MinecraftServerState.Stopped).Wait();
    }

    public async Task UpdateStatus(MinecraftServerStatusWrapper? status = null, MinecraftServerState newState = MinecraftServerState.Unknown)
    {
        status ??= await CraftyControl.Instance.GetServerStatusAsync(ServerInfo.Id);
        newState = newState == MinecraftServerState.Unknown ? State : newState;
        if (status == null)
        {
            State = MinecraftServerState.Unknown;
            await DiscordClient.UpdateStatus(false, "Can't update server status");
            return;
        }
        Status = status;
        await UpdateState(newState);

        // Sanity check for state
        if (State == MinecraftServerState.Stopped != !Status.Running)
        {
            // Can autofix when not running
            if (!Status.Running)
            {
                await UpdateState(MinecraftServerState.Stopped);
                return;
            }

            // await DiscordClient.UpdateStatus(true, "Warning: Server state is inconsistent: " + State.ToString());
            Console.WriteLine("Warning: Server state is inconsistent: " + State.ToString() + " Running: " + Status.Running);
        }
    }

    public void DisposeSocket()
    {
        WebSocket = null;
    }

    private async Task UpdateState(MinecraftServerState newState)
    {
        var oldState = State;
        State = newState;
        switch (State)
        {
            case MinecraftServerState.Running:
                WebSocket ??= await CraftyControl.Instance.CreateWebSocketAsync(ServerInfo.Id);
                break;
            case MinecraftServerState.Stopped:
                WebSocket = null;
                break;
        }

        if (oldState != State)
            StateChanged.Invoke(this, new ServerStateChangeEventArgs(this));
    }

    private async Task ListenToWebSocket()
    {
        if (WebSocket == null)
            return;

        var buffer = new byte[1024 * 4];
        while (true)
        {
            if (WebSocket == null || WebSocket.State != WebSocketState.Open)
            {
                if (Status.Running)
                    WebSocket = await CraftyControl.Instance.CreateWebSocketAsync(ServerInfo.Id);
                else
                    return;
            }

            WebSocketReceiveResult result;
            try
            {
                result = await WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            catch (Exception e)
            {
                if (e is not WebSocketException && e is not TaskCanceledException)
                    throw;

                Console.WriteLine(e.Message);
                // Reconnect if server is still online
                WebSocket?.Abort();
                if (Status.Running)
                    WebSocket = await CraftyControl.Instance.CreateWebSocketAsync(ServerInfo.Id);
                continue;
            }
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                WebSocket = null;
                return;
            }
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var messageObject = JsonConvert.DeserializeObject<SocketEventWrapper>(message);
            switch (messageObject?.Event)
            {
                case "vterm_new_line":
                    var terminalLine = (messageObject.Data as JObject)!.ToObject<TerminalLine>();
                    if (terminalLine!.Line.Contains("[Server thread/INFO]</span>: Stopping server"))
                    {
                        await UpdateState(MinecraftServerState.Stopping);
                        return;
                    }
                    break;
                case "update_server_details":
                    try
                    {
                        var serverDetails = (messageObject.Data as JObject)!.ToObject<ServerDetailUpdate>();

                        if (serverDetails!.Running && State == MinecraftServerState.Stopped || State == MinecraftServerState.Stopping || State == MinecraftServerState.Unknown)
                        {
                            if (State == MinecraftServerState.Running)
                                await UpdateState(serverDetails.OnlineCount > 0 ? MinecraftServerState.Running : MinecraftServerState.Idle);
                        }
                        else if (!serverDetails.Running && State != MinecraftServerState.Stopped)
                        {
                            await UpdateState(MinecraftServerState.Stopped);
                        }

                        if (serverDetails.OnlineCount > 0)
                            LastPlayerTime = DateTime.UtcNow;
                        else if (serverDetails.PlayersCache.Count() > 0)
                        {
                            var lastPlayer = serverDetails.PlayersCache.OrderByDescending(p => p.LastSeen).First();
                            LastPlayerTime = lastPlayer.LastSeenDateTime;
                        }
                        else
                        {
                            LastPlayerTime = null;
                        }

                        TimeSpan? timeSinceLastPlayer = DateTime.UtcNow - LastPlayerTime;
                        TimeSpan? timeSinceStarted = DateTime.UtcNow - serverDetails.Started;
                        double hoursIdle = Math.Min(timeSinceLastPlayer?.TotalHours ?? double.MaxValue, timeSinceStarted?.TotalHours ?? double.MaxValue);
                        IdleHours = hoursIdle;

                        if (hoursIdle > 1) //TODO: Make this configurable
                        {
                            await CraftyControl.Instance.StopServer(this);
                        }
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        break;
                    }
            }
        }
    }
}

public enum MinecraftServerState
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Idle, // Running but no players for a while (Default 1 hour)
    Stopping,
}