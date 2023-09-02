namespace Minecraft_Server_Manager.CraftyControl;

public class ServerStateChangeEventArgs : EventArgs {
    public MinecraftServer Server;
    public MinecraftServerState NewState;

    public ServerStateChangeEventArgs(MinecraftServer server) {
        Server = server;
        NewState = server.State;
    }
}