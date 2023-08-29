namespace Minecraft_Server_Bot.CraftyControl;

public class ServerStateChangeEventArgs : EventArgs {
    public MinecraftServer Server;
    public MinecraftServerState NewState;

    public ServerStateChangeEventArgs(MinecraftServer server) {
        Server = server;
        NewState = server.State;
    }
}