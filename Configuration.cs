using System.Configuration;

namespace Minecraft_Server_Bot;

public abstract class ConfigurationHandler
{
    private static ConfigurationHandler? _instance;

    public static ConfigurationHandler GetInstance()
    {
        return _instance;
    }

    public abstract void Save(Configuration config);
    protected void SaveValue(Configuration config, string key, string value)
    {
        if (config.AppSettings.Settings[key] == null)
            config.AppSettings.Settings.Add(key, value);
        else
            config.AppSettings.Settings[key].Value = value;
    }
}