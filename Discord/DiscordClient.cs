using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;

namespace Minecraft_Server_Manager.Discord;

class DiscordClient
{
    private readonly static DiscordSocketClient _socketClient = new(new DiscordSocketConfig()
    {
        LogLevel = LogSeverity.Debug,
    });
    private readonly static InteractionService _interactionService = new(_socketClient, new InteractionServiceConfig()
    {
        AutoServiceScopes = true,
        DefaultRunMode = RunMode.Async,
        EnableAutocompleteHandlers = true,
        ExitOnMissingModalField = false,
#if DEBUG
        ThrowOnError = true,
#else
        ThrowOnError = false,
#endif
        UseCompiledLambda = true,
    });

    private static IDMChannel? _ownerChannel;

    public static async Task Initialize(string token)
    {
        _socketClient.Log += Log;

        await _socketClient.LoginAsync(TokenType.Bot, token);

        _socketClient.Ready += ClientReadyAsync;

        await _socketClient.StartAsync();
    }

    private static async Task ClientReadyAsync()
    {
        if (_socketClient == null)
            throw new InvalidOperationException("Socket client is null");

        var added = await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), null);
        Console.WriteLine($"Added {added.Count()} modules");
        // await _interactionService.RegisterCommandsToGuildAsync(773278405042896927);
        await _interactionService.RegisterCommandsGloballyAsync();
        _socketClient.InteractionCreated += HandleInteractionAsync;

        var application = await _socketClient.GetApplicationInfoAsync();
        _ownerChannel = await application.Owner.CreateDMChannelAsync();
        _timer.Elapsed += async (sender, e) => await UpdateStatus();

        await UpdateStatus();

        _socketClient.Ready -= ClientReadyAsync;
        _socketClient.Ready += async () => await UpdateStatus();
    }

    private static async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        if (_interactionService == null)
            throw new InvalidOperationException("Interaction service is null");

        var context = new SocketInteractionContext(_socketClient, interaction);

        await _interactionService.ExecuteCommandAsync(context, null);
    }

    private static System.Timers.Timer _timer = new()
    {
        Interval = 1000 * 60 * 5,
        AutoReset = false,
        Enabled = false,
    };
    public static async Task UpdateStatus(bool working = true, string? message = null)
    {
        if (_ownerChannel == null)
            throw new InvalidOperationException("Owner channel is null");

        if (working)
        {
            _timer.Stop();
            if (_socketClient.Status != UserStatus.Online)
            {
                var statusOp = _socketClient.SetStatusAsync(UserStatus.Online);
                await _socketClient.SetCustomStatusAsync("Managing Minecraft Servers");
                await statusOp;
            }

            if (message != null)
                await _ownerChannel.SendMessageAsync(message);
        }
        else
        {
            Task statusOp = Task.CompletedTask;
            Task customStatusOp = Task.CompletedTask;
            if (_socketClient.Status != UserStatus.DoNotDisturb)
            {
                statusOp = _socketClient.SetStatusAsync(UserStatus.DoNotDisturb);
                customStatusOp = _socketClient.SetCustomStatusAsync("Not Managing Minecraft Servers");
            }
            await _ownerChannel.SendMessageAsync("Something's not working pls fix" + (message == null ? "" : $": {message}"));
            await statusOp;
            await customStatusOp;

            _timer.Stop();
            _timer.Start();
        }
    }

    private static Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }
}
