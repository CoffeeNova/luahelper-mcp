namespace LuaHelperMcpServer.Services;

public interface IProcessHandle : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    Stream StandardInput { get; }
    Stream StandardOutput { get; }
    StreamReader StandardError { get; }
    bool Start();
    void EnableRaisingEvents();
    event EventHandler Exited;
    Task WaitForExitAsync(CancellationToken cancellationToken);
    void WaitForExit(int millisecondsTimeout);
    void Kill(bool entireProcessTree);
}
