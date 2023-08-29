using Newtonsoft.Json;

namespace Minecraft_Server_Bot.CraftyControl;

public record MinecraftServerStatusWrapper {
    public bool Running { get; set; }

    [JsonProperty("server_id")]
    public required MinecraftServerWrapper ServerInfo;

    [JsonProperty("started")]
    private object? _started;
    public DateTime? Started => _started is string s ? DateTime.Parse(s) : null;

    [JsonProperty("desc")]
    public string? Description;

    [JsonProperty("online")]
    private object? _online;
    public int OnlineCount => _online is int i ? i : 0;

    [JsonProperty("max")]
    private object? _max;
    public int MaxPlayers => _max is int i ? i : 0;
}
