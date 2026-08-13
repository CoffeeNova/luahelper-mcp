using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Integration.Services;

public class ProcessManagerTests
{
    private static string CmdExe => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Test]
    public async Task EnsureRunningAsync_SpawnsProcess()
    {
        // Arrange
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");

        // Act
        await manager.EnsureRunningAsync();

        // Assert
        manager.IsRunning.ShouldBeTrue();
    }

    [Test]
    public async Task EnsureRunningAsync_AlreadyRunning_ReturnsExisting()
    {
        // Arrange
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        var firstProcess = await manager.GetProcessAsync();

        // Act
        await manager.EnsureRunningAsync();
        var secondProcess = await manager.GetProcessAsync();

        // Assert
        secondProcess.ShouldBeSameAs(firstProcess);
    }

    [Test]
    public async Task ProcessExited_EventFires()
    {
        // Arrange
        using var manager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            CmdExe,
            "/c \"echo quick\""
        );
        var exitedTcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        manager.ProcessExited += (_, _) => exitedTcs.TrySetResult();

        // Act
        await manager.EnsureRunningAsync();
        await exitedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        manager.IsRunning.ShouldBeFalse();
    }

    [Test]
    public async Task ShutdownAsync_GracefulExit()
    {
        // Arrange
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        manager.IsRunning.ShouldBeTrue();

        // Act
        await manager.ShutdownAsync();

        // Assert
        manager.IsRunning.ShouldBeFalse();
    }

    [Test]
    public async Task ForceKill_TerminatesProcess()
    {
        // Arrange
        using var manager = new ProcessManager(NullLogger<ProcessManager>.Instance, CmdExe, "/k");
        await manager.EnsureRunningAsync();
        manager.IsRunning.ShouldBeTrue();

        // Act
        manager.ForceKill();

        // Assert
        manager.IsRunning.ShouldBeFalse();
    }
}
