namespace Minecraft_Server_Bot.CraftyControl;

record SocketEventWrapper {
    public required string Event { get; set; }
    public required object Data { get; set; }
}