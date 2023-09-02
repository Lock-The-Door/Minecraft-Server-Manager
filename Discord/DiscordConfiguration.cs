using System.Configuration;

namespace Minecraft_Server_Manager.Discord;

public class DiscordConfiguration
{
    private static DiscordConfiguration? _instance;
    public static DiscordConfiguration Instance => _instance ??= new();

    public string Token { get; set; } = string.Empty;

    public void Save()
    {
        ConfigurationManager.AppSettings["Discord:Token"] = Token;
    }
    private DiscordConfiguration()
    {
        Token = ConfigurationManager.AppSettings["Discord:Token"] ?? string.Empty;
    }
}