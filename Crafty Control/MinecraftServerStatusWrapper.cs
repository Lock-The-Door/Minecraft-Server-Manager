using System.Globalization;
using Newtonsoft.Json;

namespace Minecraft_Server_Manager.CraftyControl;

public record MinecraftServerStatusWrapper {
    public required bool Running { get; set; }

    [JsonProperty("server_id")]
    public required MinecraftServerWrapper ServerInfo { get; set; }

    [JsonProperty("started")]
    private object? _started;
    public DateTime? Started 
    {
        get => _started is string s ? DateTime.Parse(s, CultureInfo.InvariantCulture) : null;
        set => _started = value;
    }

    [JsonProperty("desc")]
    public string? Description { get; set; }

    [JsonProperty("online")]
    private object? _online;
    public long OnlineCount 
    {
        get => _online as long? ?? 0;
        set => _online = value;
    }

    [JsonProperty("max")]
    private object? _max;
    public long MaxPlayers {
        get => _max as long? ?? 0;
        set => _max = value;
    }
}
