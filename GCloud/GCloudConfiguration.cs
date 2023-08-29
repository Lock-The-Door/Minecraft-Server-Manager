using System.Configuration;

namespace Minecraft_Server_Bot.GCloud;

public class GCloudConfiguration
{
    private static GCloudConfiguration? _instance;
    public static GCloudConfiguration Instance => _instance ??= new();

    public string ProjectID { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string InstanceID { get; set; } = string.Empty;

    public void Save()
    {
        ConfigurationManager.AppSettings["GCloud:ProjectID"] = ProjectID;
        ConfigurationManager.AppSettings["GCloud:Zone"] = Zone;
        ConfigurationManager.AppSettings["GCloud:InstanceID"] = InstanceID;
    }
    private GCloudConfiguration()
    {
        ProjectID = ConfigurationManager.AppSettings["GCloud:ProjectID"] ?? string.Empty;
        Zone = ConfigurationManager.AppSettings["GCloud:Zone"] ?? string.Empty;
        InstanceID = ConfigurationManager.AppSettings["GCloud:InstanceID"] ?? string.Empty;
    }
}