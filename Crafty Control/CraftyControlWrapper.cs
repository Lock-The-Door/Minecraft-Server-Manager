using Newtonsoft.Json.Linq;

namespace Minecraft_Server_Bot.CraftyControl;

public class CraftyControlWrapper {
    
    public required string Status { get; set; }
    public string? Error { get; set; }
    public string? Info { get; set; }

    // Should be JObject or JArray
    public object? Data { get; set; }
}