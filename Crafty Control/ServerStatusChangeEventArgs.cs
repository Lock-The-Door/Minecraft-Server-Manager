namespace Minecraft_Server_Manager.CraftyControl;

public class ServerStateChangeEventArgs : EventArgs {
    public MinecraftServer Server { get; set; }
    public MinecraftServerState NewState { get; set; }

    public ServerStateChangeEventArgs(MinecraftServer server) {
        Server = server;
        NewState = server.State;
    }
}