using System.Configuration;
using Minecraft_Server_Bot.CraftyControl;
using Minecraft_Server_Bot.Discord;
using Minecraft_Server_Bot.GCloud;

namespace Minecraft_Server_Bot;

static class Program
{
    static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();
    // static void Main(string[] args)
    // {
    //     Configuration configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
    //     CraftyControlConfiguration.Instance.Save(configFile);
    //     DatabaseConfiguration.Instance.Save();
    //     DiscordConfiguration.Instance.Save();
    //     GCloudConfiguration.Instance.Save();
    //     configFile.Save(ConfigurationSaveMode.Modified);
    // }

    static async Task MainAsync(string[] args)
    {
        // Handle Events
        CraftyControl.CraftyControl.Instance.ServerStopped += HandleServerStop;

        _ = DiscordClient.Initialize("OTI2ODYxMTAzOTAyOTA4NDM2.GoN_Ii.1dtppu0vtC2Ms5dIrcUMbNPpRxC8O0OyVIVVfM");

        // Block until exit
        await Task.Delay(-1);
    }

    // Poll Minecraft Servers to detect idle servers
    static async Task PollMinecraftServers()
    {
        var servers = await CraftyControl.CraftyControl.Instance.FetchServersAsync();
        if (servers == null)
            return;

        foreach (var server in servers)
        {
        }
    }

    // Turn off GCloud instance if all servers are off/idle
    static async Task HandleServerStop(object? source, ServerStateChangeEventArgs stoppedServer)
    {
        // Check if all servers off
        await CraftyControl.CraftyControl.Instance.UpdateServerStatuses();
        List<MinecraftServer> servers = CraftyControl.CraftyControl.Instance.MinecraftServers;
        if (servers == null || servers.Count == 0)
        {
            await GCloudManager.Instance.StopInstance();
            return;
        }

        foreach (var server in servers)
        {
            switch (server.State)
            {
                case MinecraftServerState.Running:
                case MinecraftServerState.Starting:
                    return;
                case MinecraftServerState.Unknown:
                    CraftyControl.CraftyControl.Instance.UpdateServerStatuses().Wait();
                    return;
                case MinecraftServerState.Idle:
                    if (server.IdleHours < 1)
                        return;
                    break;
                default:
                    break;
            }
        }

        Console.WriteLine("Stopping GCloud instance: All servers are off");
        await GCloudManager.Instance.StopInstance();
    }
}
