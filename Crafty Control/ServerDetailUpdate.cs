using System.Globalization;
using Newtonsoft.Json;

namespace Minecraft_Server_Manager.CraftyControl;

record ServerDetailUpdate {
    #pragma warning disable CS0649
    [JsonProperty("id")]
    public required int ServerId;
    [JsonProperty("started")]
    private object? _started;
    public DateTime? Started
    {
        get => _started is string s ? DateTime.Parse(s, CultureInfo.InvariantCulture) : null;
        set => _started = value;
    }
    #pragma warning disable CS0649
    [JsonProperty("running")]
    public required bool Running;
    [JsonProperty("online")]
    private object? _online;
    public long OnlineCount
    {
        get => _online as long? ?? 0;
        set => _online = value;
    }
    [JsonProperty("max")]
    private object? _max;
    public long MaxPlayers 
    {
        get => _max as long? ?? 0;
        set => _max = value;
    }
    [JsonProperty("players_cache")]
    public PlayerCacheData[] PlayersCache = Array.Empty<PlayerCacheData>();
}