using System.Diagnostics;

namespace LuaHelperMcpServer.Services;

public sealed class ProcessHandle : IProcessHandle
{
    private readonly Process _process;

    public ProcessHandle(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public int Id => _process.Id;

    public bool HasExited => _process.HasExited;

    public Stream StandardInput => _process.StandardInput.BaseStream;

    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    public StreamReader StandardError => _process.StandardError;

    public event EventHandler Exited
    {
        add => _process.Exited += value;
        remove => _process.Exited -= value;
    }

    public bool Start() => _process.Start();

    public void EnableRaisingEvents() => _process.EnableRaisingEvents = true;

    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _process.WaitForExitAsync(cancellationToken);

    public void WaitForExit(int millisecondsTimeout) => _process.WaitForExit(millisecondsTimeout);

    public void Kill(bool entireProcessTree) => _process.Kill(entireProcessTree);

    public void Dispose() => _process.Dispose();
}
