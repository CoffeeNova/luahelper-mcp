using LuaHelperMcpServer.Services;

namespace LuaHelperMcpServer.Tests.Unit.Helpers;

public sealed class MockProcessManager : IProcessManager
{
    private readonly FakeLspServer _fakeServer;

    public bool IsRunning => true;
    public int EnsureRunningAsyncCalls { get; private set; }
    public event EventHandler? ProcessExited;

    public MockProcessManager(FakeLspServer fakeServer)
    {
        _fakeServer = fakeServer;
    }

    public Task EnsureRunningAsync(CancellationToken ct = default)
    {
        EnsureRunningAsyncCalls++;
        return Task.CompletedTask;
    }

    public Task<IProcessHandle> GetProcessAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("Use GetStreams() instead");
    }

    public (Stream stdin, Stream stdout) GetStreams()
    {
        return (_fakeServer.ClientStdin, _fakeServer.ClientStdout);
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        ProcessExited?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void ForceKill()
    {
        ProcessExited?.Invoke(this, EventArgs.Empty);
    }
}
