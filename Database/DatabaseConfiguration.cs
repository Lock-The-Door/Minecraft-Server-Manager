using System.Configuration;

namespace Minecraft_Server_Manager.Database;

public class DatabaseConfiguration
{
    private static DatabaseConfiguration? _instance;
    public static DatabaseConfiguration Instance => _instance ??= new();

    public string Source { get; set; } = "localhost";
    public string UserId { get; set; } = "sa";
    public string Password { get; set; } = string.Empty;

    public void Save()
    {
        ConfigurationManager.AppSettings["Database:Source"] = Source;
        ConfigurationManager.AppSettings["Database:UserId"] = UserId;
        ConfigurationManager.AppSettings["Database:Password"] = Password;
    }
    private DatabaseConfiguration()
    {
        Source = ConfigurationManager.AppSettings["Database:Source"] ?? "localhost";
        UserId = ConfigurationManager.AppSettings["Database:UserId"] ?? "sa";
        Password = ConfigurationManager.AppSettings["Database:Password"] ?? string.Empty;
    }
}