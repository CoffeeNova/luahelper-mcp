using System.Diagnostics;
using System.Text;
using LuaHelperMcpServer.Services;

namespace LuaHelperMcpServer.Tests.Unit.Helpers;

/// <summary>
/// An in-memory <see cref="IProcessHandle" /> with manual exit control.
/// </summary>
internal sealed class FakeProcessHandle : IProcessHandle
{
    private readonly Func<Task> _waitForExitAsync;
    private readonly Action<int> _onWaitForExit;
    private readonly Action<bool> _onKill;
    private readonly Stream _stdin;
    private readonly Stream _stdout;

    public FakeProcessHandle(
        Stream? stdin = null,
        Stream? stdout = null,
        Func<Task>? waitForExitAsync = null,
        Action<int>? onWaitForExit = null,
        Action<bool>? onKill = null
    )
    {
        _stdin = stdin ?? new MemoryStream();
        _stdout = stdout ?? new MemoryStream();
        StandardError = new StreamReader(
            new MemoryStream(Encoding.UTF8.GetBytes("")),
            leaveOpen: true
        );
        _waitForExitAsync = waitForExitAsync ?? (() => Task.CompletedTask);
        _onWaitForExit = onWaitForExit ?? (_ => { });
        _onKill = onKill ?? (_ => { });
    }

    public int Id { get; set; } = 42;

    public bool HasExited { get; set; }

    public int StartCallCount { get; private set; }

    public bool EnableRaisingEventsCalled { get; private set; }

    public int WaitForExitTimeoutMilliseconds { get; private set; } = -1;

    public bool KillCalled { get; private set; }

    public bool KillEntireProcessTree { get; private set; }

    public bool DisposeCalled { get; private set; }

    public Stream StandardInput => _stdin;

    public Stream StandardOutput => _stdout;

    public StreamReader StandardError { get; }

    public event EventHandler? Exited;

    public bool Start()
    {
        StartCallCount++;
        return true;
    }

    public void EnableRaisingEvents() => EnableRaisingEventsCalled = true;

    public Task WaitForExitAsync(CancellationToken cancellationToken) => _waitForExitAsync();

    public void WaitForExit(int millisecondsTimeout)
    {
        WaitForExitTimeoutMilliseconds = millisecondsTimeout;
        _onWaitForExit(millisecondsTimeout);
    }

    public void Kill(bool entireProcessTree)
    {
        KillCalled = true;
        KillEntireProcessTree = entireProcessTree;
        HasExited = true;
        _onKill(entireProcessTree);
    }

    public void RaiseExited()
    {
        HasExited = true;
        Exited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        DisposeCalled = true;
        _stdin.Dispose();
        _stdout.Dispose();
        StandardError.Dispose();
    }
}

/// <summary>
/// An in-memory <see cref="IProcessLauncher" /> that records created handles.
/// </summary>
internal sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<FakeProcessHandle> Created { get; } = [];

    public ProcessStartInfo? LastStartInfo { get; private set; }

    public FakeProcessHandle Last => Created[^1];

    public IProcessHandle Start(ProcessStartInfo startInfo)
    {
        LastStartInfo = startInfo;
        var handle = new FakeProcessHandle();
        Created.Add(handle);
        return handle;
    }
}
