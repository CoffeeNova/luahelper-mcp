using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LuaHelperMcpServer.Tests.Integration.Services;

public class ProcessManagerTests
{
    private static string CmdExe => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Test]
    public async Task EnsureRunningAsync_SpawnsProcess()
    {
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        Assert.That(manager.IsRunning, Is.True);
    }

    [Test]
    public async Task EnsureRunningAsync_AlreadyRunning_ReturnsExisting()
    {
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        var firstProcess = await manager.GetProcessAsync();

        await manager.EnsureRunningAsync();
        var secondProcess = await manager.GetProcessAsync();

        Assert.That(secondProcess, Is.SameAs(firstProcess));
    }

    [Test]
    public async Task ProcessExited_EventFires()
    {
        using var manager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            CmdExe,
            "/c \"echo quick\""
        );
        var exitedTcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        manager.ProcessExited += (_, _) => exitedTcs.TrySetResult();

        await manager.EnsureRunningAsync();
        await exitedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(manager.IsRunning, Is.False);
    }

    [Test]
    public async Task ShutdownAsync_GracefulExit()
    {
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        Assert.That(manager.IsRunning, Is.True);

        await manager.ShutdownAsync();

        Assert.That(manager.IsRunning, Is.False);
    }

    [Test]
    public async Task ForceKill_TerminatesProcess()
    {
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        Assert.That(manager.IsRunning, Is.True);

        manager.ForceKill();

        Assert.That(manager.IsRunning, Is.False);
    }
}
