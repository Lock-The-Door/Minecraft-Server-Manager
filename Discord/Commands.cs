using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Minecraft_Server_Bot.CraftyControl;
using Minecraft_Server_Bot.Database;
using Minecraft_Server_Bot.GCloud;

namespace Minecraft_Server_Bot.Discord;

public class Commands : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("list-servers", "Lists all Minecraft servers")]
    public async Task ListServersAsync()
    {
        _ = DeferAsync();
        var fetchOp = CraftyControl.CraftyControl.Instance.FetchServersAsync()
            .ContinueWith(async servers => servers.Result != null ? await DatabaseApi.Instance.FilterMinecraftServers(servers.Result, Context.User.Id, Context.Guild?.Id) : null);

        // Build embed
        var embed = new EmbedBuilder();
        embed.WithTitle("Minecraft Servers");
        embed.WithDescription("Here is a list of Minecraft servers");
        embed.WithColor(Color.Blue);

        // Filter servers
        var filteredServers = await fetchOp.Unwrap();

        if (filteredServers == null)
        {
            embed.WithDescription("There was an error fetching the server list. Please try again later.");
            embed.WithColor(Color.Red);
            await ModifyOriginalResponseAsync(original => original.Embed = embed.Build());
            await DiscordClient.UpdateStatus(false, "Can't fetch server list");
            return;
        }

        foreach (var server in filteredServers)
        {
            var minDelay = Task.Delay(100);
            var serverStats = await CraftyControl.CraftyControl.Instance.GetServerStatusAsync(server.Id);

            if (serverStats == null)
            {
                embed.WithDescription("There was an error fetching the server list. Please try again later.");
                embed.WithColor(Color.Red);
                embed.Fields.Clear();
                await ModifyOriginalResponseAsync(original => original.Embed = embed.Build());
                await DiscordClient.UpdateStatus(false, "Can't fetch server list");
                return;
            }

            var embedField = new EmbedFieldBuilder();
            embedField.WithName($"{server.Name} (Id: {server.Id})");
            if (serverStats.Description != "False")
                embedField.Value = serverStats.Description + "\n";

            if (serverStats.Running)
                embedField.Value += $"Status: Online\nPlayers: {serverStats.OnlineCount}/{serverStats.MaxPlayers}";
            else
                embedField.Value += $"Status: Offline";

            embedField.Value += "\nPort: " + server.Port;

            embedField.WithIsInline(false);

            embed.AddField(embedField);

            await minDelay;
        }

        await ModifyOriginalResponseAsync(original => original.Embed = embed.Build());
    }

    [SlashCommand("start-server", "Starts the Minecraft server")]
    public async Task StartServerAsync([Summary(description: "The id of the server to start"), Autocomplete(typeof(ServerNameAutocompleter))] int serverId)
    {
        await DeferAsync();

        // Send start command
        var startOp = CraftyControl.CraftyControl.Instance.StartServer(serverId);
        var gcloudOp = GCloudManager.Instance.StartInstance();

        // Start getting details for server (name, and in the future port)
        var serverStats = await CraftyControl.CraftyControl.Instance.GetServerStatusAsync(serverId);
        if (serverStats?.ServerInfo == null)
        {
            await RespondAsync("Couldn't find the server. Please try again later.", ephemeral: true);
            await DiscordClient.UpdateStatus(false, "Can't fetch server list");
            return;
        }

        // Build embed
        var embed = new EmbedBuilder();
        embed.WithTitle($"Starting {serverStats.ServerInfo.Name}...");
        embed.WithColor(Color.Gold);
        embed.AddField("Server ID", serverStats.ServerInfo.Id);
        embed.AddField("Port", serverStats.ServerInfo.Port);
        embed.WithFooter("Server UUID: " + serverStats.ServerInfo.UUID);
        await ModifyOriginalResponseAsync(original => original.Embed = embed.Build());

        // Wait for server to finish starting
        var startResult = await startOp;
        await gcloudOp;

        // Update embed
        if (startResult)
        {
            embed.WithTitle($"{serverStats.ServerInfo.Name} Started");
            string serverIp = GCloudManager.Instance.ExternalIp ?? "";
            embed.WithDescription($"Connect at `{serverIp}:{serverStats.ServerInfo.Port}`");
            embed.WithColor(Color.Green);
        } else {
            embed.WithTitle($"{serverStats.ServerInfo.Name} Failed to Start");
            embed.WithDescription("The server failed to start or took too long to start. Please try again later.");
            embed.WithColor(Color.Red);
            await DiscordClient.UpdateStatus(false, "Server's are failing to start");
        }
        await ModifyOriginalResponseAsync(original => original.Embed = embed.Build());
    }

    [SlashCommand("reset-status", "Resets the bot's status back to normal")]
    [RequireOwner]
    public async Task ResetStatusAsync()
    {
        await DiscordClient.UpdateStatus(true);
        await RespondAsync("Status reset", ephemeral: true);
    }
}

public class ServerNameAutocompleter : AutocompleteHandler
{
    public static readonly Regex _serverIdRegex = new(@".*\((\d)\)", RegexOptions.Compiled);

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        // Current value
        var data = autocompleteInteraction.Data as SocketAutocompleteInteractionData;
        var current = data?.Current;
        var currentValue = (current?.Value ?? "").ToString()!.ToLower().Trim();

        // Fetch servers
        List<MinecraftServerWrapper>? servers = await CraftyControl.CraftyControl.Instance.FetchServersAsync();
        if (servers == null)
            return AutocompletionResult.FromError(new Exception("There was an error fetching the server list. Please try again later"));
        List<MinecraftServerWrapper> filteredServers = await DatabaseApi.Instance.FilterMinecraftServers(servers, context.User.Id, context.Guild?.Id);
        // TODO: Once the list all flag is implemented, give the non-filtered list a autocomplete boost

        // Create score list and sort
        Dictionary<string, int> scores = filteredServers.ToDictionary(server => $"{server.Name.ToLower().Trim()} ({server.Id})", server => 0);
        var words = currentValue.Split(' ');
        foreach (string currentWord in words)
        {
            foreach (KeyValuePair<string, int> serverScore in scores)
            {
                var serverName = serverScore.Key;

                if (serverName.StartsWith(currentWord))
                    scores[serverScore.Key] += 1;
                if (serverName.Contains(currentWord))
                    scores[serverScore.Key] += 1;
                if (serverName.EndsWith($"({currentWord})"))
                    scores[serverScore.Key] += 2;
            }
        }

        var sorted = scores.OrderBy(scores => scores.Key).OrderByDescending(score => score.Value);
        var results = sorted.Select(score => new AutocompleteResult(score.Key, int.Parse(_serverIdRegex.Match(score.Key).Groups[1].Value))).Take(25);

        return AutocompletionResult.FromSuccess(results);
    }
}