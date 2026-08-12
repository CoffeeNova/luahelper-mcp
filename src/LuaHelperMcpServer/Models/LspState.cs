namespace LuaHelperMcpServer.Models;

public enum LspState
{
    NotStarted,
    Spawning,
    Initializing,
    Ready,
    OpeningFiles,
    CollectingDiagnostics,
    Crashed,
    WaitingBackoff,
    Failed,
    ShuttingDown,
    Stopped,
}
