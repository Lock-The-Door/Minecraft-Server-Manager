using System.Configuration;

namespace Minecraft_Server_Manager.CraftyControl;

public class CraftyControlConfiguration
{
    private static CraftyControlConfiguration? _instance;
    public static CraftyControlConfiguration Instance => _instance ??= new CraftyControlConfiguration();

    public Uri BaseAddress { get; set; } = new Uri("https://localhost:8443/");
    public string Token { get; set; } = string.Empty;

    private CraftyControlConfiguration() { 
        BaseAddress = new Uri(ConfigurationManager.AppSettings["CraftyControl:BaseAddress"] ?? "https://fw-120.pug-squeaker.ts.net:25443");
        Token = ConfigurationManager.AppSettings["CraftyControl:Token"] ?? "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJ1c2VyX2lkIjozLCJpYXQiOjE2OTI5NDY0OTEsInRva2VuX2lkIjoxfQ.GOpxZrNQKAwqIsg3uSl9zMsfZsiMepXn540pKkMMNzo";
    }
    public void Save(Configuration config)
    {
        if (config.AppSettings.Settings["CraftyControl:BaseAddress"] == null)
            config.AppSettings.Settings.Add("CraftyControl:BaseAddress", BaseAddress.ToString());
        else
            config.AppSettings.Settings["CraftyControl:BaseAddress"].Value = BaseAddress.ToString();
            
        ConfigurationManager.AppSettings["CraftyControl:Token"] = Token;
    }
}