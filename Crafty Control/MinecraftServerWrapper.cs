using Newtonsoft.Json;

namespace Minecraft_Server_Manager.CraftyControl;

public record MinecraftServerWrapper {
    [JsonProperty("server_id")]
    public int Id { get; set; }

    [JsonProperty("server_uuid")]
    public Guid UUID { get; set; }

    [JsonProperty("server_name")]
    public required string Name { get; set; }

    [JsonProperty("server_port")]
    public int Port { get; set; }
}