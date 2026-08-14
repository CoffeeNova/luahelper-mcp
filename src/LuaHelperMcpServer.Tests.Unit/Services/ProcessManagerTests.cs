using System.Diagnostics;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class ProcessManagerTests
{
    private static string ExistingExePath => typeof(ProcessManagerTests).Assembly.Location;

    private static string MissingExePath => "C:\\nonexistent\\lualsp.exe";

    private FakeProcessLauncher _launcher = null!;
    private ProcessManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _launcher = new FakeProcessLauncher();
    }

    [TearDown]
    public void TearDown()
    {
        _manager.Dispose();
    }

    private ProcessManager CreateManager(int maxRestarts = 3, TimeSpan[]? backoffSchedule = null) =>
        new(
            NullLogger<ProcessManager>.Instance,
            ExistingExePath,
            maxRestarts: maxRestarts,
            backoffSchedule: backoffSchedule,
            launcher: _launcher
        );

    [Test]
    public async Task EnsureRunningAsync_NotRunning_SpawnsViaLauncher()
    {
        // Arrange
        _manager = CreateManager();

        // Act
        await _manager.EnsureRunningAsync();

        // Assert
        _manager.IsRunning.ShouldBeTrue();
        _launcher.Created.Count.ShouldBe(1);
        _launcher.LastStartInfo.ShouldNotBeNull();
        _launcher.LastStartInfo.FileName.ShouldBe(ExistingExePath);
        _launcher.LastStartInfo.Arguments.ShouldBe("-mode=1 -logflag=0");
        _launcher.LastStartInfo.UseShellExecute.ShouldBeFalse();
        _launcher.LastStartInfo.RedirectStandardInput.ShouldBeTrue();
        _launcher.LastStartInfo.RedirectStandardOutput.ShouldBeTrue();
        _launcher.LastStartInfo.RedirectStandardError.ShouldBeTrue();
        _launcher.LastStartInfo.CreateNoWindow.ShouldBeTrue();
        _launcher.Last.EnableRaisingEventsCalled.ShouldBeTrue();
    }

    [Test]
    public async Task EnsureRunningAsync_AlreadyRunning_DoesNotSpawnAgain()
    {
        // Arrange
        _manager = CreateManager();
        await _manager.EnsureRunningAsync();

        // Act
        await _manager.EnsureRunningAsync();

        // Assert
        _launcher.Created.Count.ShouldBe(1);
    }

    [Test]
    public async Task EnsureRunningAsync_ExeMissing_ThrowsFileNotFoundException()
    {
        // Arrange
        _manager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            MissingExePath,
            launcher: _launcher
        );

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(() => _manager.EnsureRunningAsync());
        _launcher.Created.Count.ShouldBe(0);
    }

    [Test]
    public async Task EnsureRunningAsync_AfterMaxRestarts_ThrowsInvalidOperation()
    {
        // Arrange
        _manager = CreateManager(maxRestarts: 2);
        await _manager.EnsureRunningAsync();
        _launcher.Last.RaiseExited();
        await _manager.EnsureRunningAsync();
        _launcher.Last.RaiseExited();
        _launcher.Last.RaiseExited();

        // Act & Assert — attempts (3) >= maxRestarts (2)
        await Should.ThrowAsync<InvalidOperationException>(() => _manager.EnsureRunningAsync());
    }

    [Test]
    public async Task EnsureRunningAsync_AfterThrow_ResetsRestartCounter()
    {
        // Arrange
        _manager = CreateManager(maxRestarts: 1);
        await _manager.EnsureRunningAsync();
        _launcher.Last.RaiseExited();
        await Should.ThrowAsync<InvalidOperationException>(() => _manager.EnsureRunningAsync());

        // Act — counter was reset to 0, next call spawns again
        await _manager.EnsureRunningAsync();

        // Assert
        _launcher.Created.Count.ShouldBe(2);
    }

    [Test]
    public async Task EnsureRunningAsync_OnRestart_AppliesBackoff()
    {
        // Arrange — a long backoff; cancellation proves the delay was awaited
        _manager = CreateManager(backoffSchedule: [TimeSpan.FromSeconds(30)]);
        await _manager.EnsureRunningAsync();
        _launcher.Last.RaiseExited();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert — Task.Delay(30s) is cancelled → OCE, not a spawn
        await Should.ThrowAsync<OperationCanceledException>(() =>
            _manager.EnsureRunningAsync(cts.Token)
        );
        _launcher.Created.Count.ShouldBe(1);
    }

    [Test]
    public async Task OnProcessExited_IncrementsRestartAttempts_AndRaisesEvent()
    {
        // Arrange
        _manager = CreateManager(maxRestarts: 1);
        await _manager.EnsureRunningAsync();
        var exitedEvent = 0;
        _manager.ProcessExited += (_, _) => exitedEvent++;

        // Act
        _launcher.Last.RaiseExited();

        // Assert
        exitedEvent.ShouldBe(1);
    }

    [Test]
    public async Task ShutdownAsync_GracefulExit_WaitsForExit()
    {
        // Arrange
        _manager = CreateManager();
        await _manager.EnsureRunningAsync();

        // Act
        await _manager.ShutdownAsync();

        // Assert
        _launcher.Last.KillCalled.ShouldBeFalse();
        _launcher.Last.WaitForExitTimeoutMilliseconds.ShouldBe(-1);
    }

    [Test]
    public async Task ShutdownAsync_Timeout_ForceKills()
    {
        // Arrange — WaitForExitAsync never completes → 5s timeout → ForceKill
        var neverCompletes = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var handle = new FakeProcessHandle(waitForExitAsync: () => neverCompletes.Task);
        _manager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            ExistingExePath,
            launcher: new FixedHandleLauncher(handle)
        );
        await _manager.EnsureRunningAsync();

        // Act
        await _manager.ShutdownAsync();

        // Assert
        handle.KillCalled.ShouldBeTrue();
        handle.KillEntireProcessTree.ShouldBeTrue();
    }

    [Test]
    public async Task ShutdownAsync_AlreadyExited_Noop()
    {
        // Arrange
        var handle = new FakeProcessHandle { HasExited = true };
        _manager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            ExistingExePath,
            launcher: new FixedHandleLauncher(handle)
        );

        // Act
        await _manager.ShutdownAsync();

        // Assert
        handle.KillCalled.ShouldBeFalse();
        handle.WaitForExitTimeoutMilliseconds.ShouldBe(-1);
    }

    [Test]
    public void ForceKill_AlreadyExited_Noop()
    {
        // Arrange
        var handle = new FakeProcessHandle { HasExited = true };
        _manager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            ExistingExePath,
            launcher: new FixedHandleLauncher(handle)
        );

        // Act
        _manager.ForceKill();

        // Assert
        handle.KillCalled.ShouldBeFalse();
    }

    [Test]
    public void ForceKill_Running_KillsAndWaits()
    {
        // Arrange
        _manager = CreateManager();
        _manager.EnsureRunningAsync().GetAwaiter().GetResult();

        // Act
        _manager.ForceKill();

        // Assert
        _launcher.Last.KillCalled.ShouldBeTrue();
        _launcher.Last.KillEntireProcessTree.ShouldBeTrue();
        _launcher.Last.WaitForExitTimeoutMilliseconds.ShouldBe(5000);
        _manager.IsRunning.ShouldBeFalse();
    }

    [Test]
    public void Dispose_RunningProcess_KillsAndDisposes()
    {
        // Arrange
        _manager = CreateManager();
        _manager.EnsureRunningAsync().GetAwaiter().GetResult();

        // Act
        _manager.Dispose();

        // Assert
        _launcher.Last.KillCalled.ShouldBeTrue();
        _launcher.Last.DisposeCalled.ShouldBeTrue();
    }

    [Test]
    public void Dispose_Idempotent()
    {
        // Arrange
        _manager = CreateManager();
        _manager.EnsureRunningAsync().GetAwaiter().GetResult();

        // Act
        _manager.Dispose();
        _manager.Dispose();

        // Assert
        _launcher.Last.DisposeCalled.ShouldBeTrue();
    }

    [Test]
    public void GetStreams_NotRunning_Throws()
    {
        // Arrange
        _manager = CreateManager();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _manager.GetStreams());
    }

    [Test]
    public async Task GetProcessAsync_NotRunning_Throws()
    {
        // Arrange
        _manager = CreateManager();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => _manager.GetProcessAsync());
    }

    [Test]
    public async Task GetProcessAsync_Running_ReturnsSameHandle()
    {
        // Arrange
        _manager = CreateManager();
        await _manager.EnsureRunningAsync();

        // Act
        var process = await _manager.GetProcessAsync();

        // Assert
        process.ShouldBeSameAs(_launcher.Last);
    }

    [Test]
    public async Task ReadStderrAsync_EndOfStream_StopsCleanly()
    {
        // Arrange — empty stderr → ReadLineAsync returns null immediately
        _manager = CreateManager();

        // Act — must complete without exception
        await _manager.EnsureRunningAsync();

        // Assert
        _manager.IsRunning.ShouldBeTrue();
    }

    private sealed class FixedHandleLauncher(FakeProcessHandle handle) : IProcessLauncher
    {
        public IProcessHandle Start(ProcessStartInfo startInfo) => handle;
    }
}
