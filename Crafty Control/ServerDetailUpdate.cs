using Newtonsoft.Json;

namespace Minecraft_Server_Manager.CraftyControl;

record ServerDetailUpdate {
    [JsonProperty("id")]
    public required int ServerId;
    [JsonProperty("started")]
    private object? _started;
    public DateTime? Started => _started is string s ? DateTime.Parse(s) : null;
    [JsonProperty("running")]
    public required bool Running;
    [JsonProperty("online")]
    private object? _online;
    public int OnlineCount => _online is int i ? i : 0;
    [JsonProperty("max")]
    private object? _max;
    public int MaxPlayers => _max is int i ? i : 0;
    [JsonProperty("players_cache")]
    public PlayerCacheData[] PlayersCache = Array.Empty<PlayerCacheData>();
}