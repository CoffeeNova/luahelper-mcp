using System.Diagnostics;

namespace LuaHelperMcpServer.Services;

public interface IProcessManager
{
    bool IsRunning { get; }
    event EventHandler? ProcessExited;
    Task EnsureRunningAsync(CancellationToken ct = default);
    Task<Process> GetProcessAsync(CancellationToken ct = default);
    (Stream stdin, Stream stdout) GetStreams();
    Task ShutdownAsync(CancellationToken ct = default);
    void ForceKill();
}
