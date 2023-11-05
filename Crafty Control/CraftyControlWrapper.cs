namespace Minecraft_Server_Manager.CraftyControl;

public class CraftyControlWrapper {
    
    public required string Status { get; set; }
    public string? Error { get; set; }
    public string? Info { get; set; }

    // Should be JObject or JArray
    public object? Data { get; set; }
}