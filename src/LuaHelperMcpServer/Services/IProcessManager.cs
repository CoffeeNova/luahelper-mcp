using LuaHelperMcpServer.Services;

namespace LuaHelperMcpServer.Services;

public interface IProcessManager
{
    bool IsRunning { get; }
    event EventHandler? ProcessExited;
    Task EnsureRunningAsync(CancellationToken ct = default);
    Task<IProcessHandle> GetProcessAsync(CancellationToken ct = default);
    (Stream stdin, Stream stdout) GetStreams();
    Task ShutdownAsync(CancellationToken ct = default);
    void ForceKill();
}
