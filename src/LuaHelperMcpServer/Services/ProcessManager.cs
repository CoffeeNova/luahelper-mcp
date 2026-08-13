using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LuaHelperMcpServer.Services;

public sealed class ProcessManager : IProcessManager, IDisposable
{
    private readonly ILogger<ProcessManager> _logger;
    private readonly string _exePath;
    private readonly string _arguments;
    private readonly int _maxRestarts;
    private readonly TimeSpan[] _backoffSchedule;
    private Process? _process;
    private bool _disposed;
    private int _restartAttempts;

    public bool IsRunning => _process is { HasExited: false };

    public event EventHandler? ProcessExited;

    public ProcessManager(
        ILogger<ProcessManager> logger,
        string exePath,
        string? arguments = null,
        int maxRestarts = 3,
        TimeSpan[]? backoffSchedule = null
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exePath = exePath ?? throw new ArgumentNullException(nameof(exePath));
        _arguments = arguments ?? "-mode=1 -logflag=0";
        _maxRestarts = maxRestarts;
        _backoffSchedule =
            backoffSchedule
            ?? [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];
    }

    public async Task EnsureRunningAsync(CancellationToken ct = default)
    {
        if (IsRunning)
            return;

        if (_restartAttempts >= _maxRestarts)
        {
            _restartAttempts = 0;
            throw new InvalidOperationException(
                $"Process at {_exePath} failed to start after {_maxRestarts} restart attempts"
            );
        }

        CleanupExistingProcess();
        VerifyExecutableExists();

        if (_restartAttempts > 0)
            await ApplyBackoffAsync(ct);

        _logger.LogInformation(
            "Spawning process {Exe} {Args} (attempt {Attempt})",
            _exePath,
            _arguments,
            _restartAttempts + 1
        );
        SpawnProcess();

        _ = Task.Run(() => ReadStderrAsync(ct), ct);
        await Task.Delay(100, ct);
        _logger.LogInformation("Process started (PID: {Pid})", _process!.Id);
    }

    private void CleanupExistingProcess()
    {
        if (_process != null)
        {
            _process.Dispose();
            _process = null;
        }
    }

    private void VerifyExecutableExists()
    {
        if (!File.Exists(_exePath))
            throw new FileNotFoundException($"Executable not found at: {_exePath}.", _exePath);
    }

    private async Task ApplyBackoffAsync(CancellationToken ct)
    {
        var index = Math.Min(_restartAttempts - 1, _backoffSchedule.Length - 1);
        var delay = _backoffSchedule[index];
        _logger.LogInformation(
            "Waiting {Delay} before restart attempt {Attempt}",
            delay,
            _restartAttempts + 1
        );
        await Task.Delay(delay, ct);
    }

    private void SpawnProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _exePath,
            Arguments = _arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        _process = new Process { StartInfo = startInfo };
        _process.EnableRaisingEvents = true;
        _process.Exited += OnProcessExited;

        if (!_process.Start())
            throw new InvalidOperationException($"Failed to start process at {_exePath}");

        _restartAttempts = 0;
    }

    public Task<Process> GetProcessAsync(CancellationToken ct = default)
    {
        if (!IsRunning)
            throw new InvalidOperationException(
                "Process is not running. Call EnsureRunningAsync first."
            );
        return Task.FromResult(_process!);
    }

    public (Stream stdin, Stream stdout) GetStreams()
    {
        if (!IsRunning || _process == null)
            throw new InvalidOperationException(
                "Process is not running. Call EnsureRunningAsync first."
            );
        return (_process.StandardInput.BaseStream, _process.StandardOutput.BaseStream);
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_process == null || _process.HasExited)
            return;

        _logger.LogInformation("Shutting down process (PID: {Pid})", _process.Id);

        try
        {
            await _process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Process did not exit gracefully, force killing");
            ForceKill();
        }
        catch (OperationCanceledException)
        {
            ForceKill();
        }
    }

    public void ForceKill()
    {
        if (_process == null || _process.HasExited)
            return;

        _logger.LogWarning("Force killing process (PID: {Pid})", _process.Id);
        _process.Kill(entireProcessTree: true);
        _process.WaitForExit(5000);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_process != null)
        {
            _process.Exited -= OnProcessExited;
            if (!_process.HasExited)
                ForceKill();
            _process.Dispose();
            _process = null;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _restartAttempts++;
        _logger.LogWarning(
            "Process exited unexpectedly (PID: {Pid}, restart attempt {Attempt})",
            _process?.Id,
            _restartAttempts
        );
        ProcessExited?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReadStderrAsync(CancellationToken ct)
    {
        try
        {
            if (_process?.StandardError == null)
                return;
            var reader = _process.StandardError;
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null)
                    break;
                _logger.LogDebug("[stderr] {Line}", line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading process stderr");
        }
    }
}
