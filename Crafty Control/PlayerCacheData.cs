using Newtonsoft.Json;

namespace Minecraft_Server_Manager.CraftyControl;

record PlayerCacheData {
    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("status")]
    public required string Status { get; set; }

    [JsonProperty("last_seen")]
    public required string LastSeen { get; set; }

    [JsonIgnore]
    public DateTime LastSeenDateTime => DateTime.ParseExact(LastSeen, "dd/MM/yyyy HH:mm", null);
}