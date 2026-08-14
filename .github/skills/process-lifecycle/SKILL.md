# Skill: External Process Lifecycle Management

Use when working with `ProcessManager` or managing any external process in .NET.

## ProcessManager architecture

```
EnsureRunningAsync()
  ├── Check IsRunning → return if already running
  ├── Check _restartAttempts >= _maxRestarts → throw
  ├── CleanupExistingProcess()
  ├── VerifyExecutableExists()
  ├── ApplyBackoffAsync() (if restarting)
  └── SpawnProcess()
        ├── Create ProcessStartInfo
        ├── Start process
        └── Reset _restartAttempts = 0
```

## Testability seam (IProcessLauncher / IProcessHandle)

`ProcessManager` spawns processes through `IProcessLauncher`
(`ProcessLauncher` is the production wrapper around
`System.Diagnostics.Process`; `ProcessHandle` wraps the `Process` members the
manager uses — `Id`, `HasExited`, streams, `Start`, events, `WaitForExit`,
`Kill`, dispose). The launcher is an optional constructor parameter defaulting
to a real `ProcessLauncher`, and is registered in DI. Unit tests inject
`FakeProcessLauncher`/`FakeProcessHandle` (in-memory streams, manual
`RaiseExited()` control) so restart/backoff/shutdown logic is testable without
a real OS process. `GetProcessAsync` returns `Task<IProcessHandle>`.

## Restart policy

- Max 3 restart attempts per crash cycle
- Exponential backoff: 2s → 4s → 8s
- After max retries: throw `InvalidOperationException`, reset counter on next call
- Backoff index is clamped to `_backoffSchedule.Length - 1` (safe if >3 attempts somehow)

## ProcessStartInfo configuration

```csharp
new ProcessStartInfo
{
    FileName = exePath,
    Arguments = "-mode=1 -logflag=0",
    UseShellExecute = false,           // Required for redirect
    RedirectStandardInput = true,      // Write LSP messages
    RedirectStandardOutput = true,     // Read LSP responses
    RedirectStandardError = true,      // Read logs (prevent deadlock)
    CreateNoWindow = true,             // No console window
};
```

## Critical: stderr deadlock prevention

Always read stderr in a background task:

```csharp
_ = Task.Run(() => ReadStderrAsync(ct), ct);
```

Without this, if the process writes enough to stderr, the buffer fills up and the process blocks — causing a deadlock.

## Graceful shutdown sequence

1. Send LSP `shutdown` request (wait 5s for response)
2. Send LSP `exit` notification
3. Cancel read loop
4. Call `processManager.ShutdownAsync()` (wait 5s for process exit)
5. If process doesn't exit: `ForceKill()` (Kill entire process tree)

## Event handling

```csharp
_process.EnableRaisingEvents = true;
_process.Exited += OnProcessExited;
```

The `ProcessExited` event fires when the process exits unexpectedly. The `LspClient` subscribes to this to detect crashes.

## Key gotchas

- `Process.Kill(entireProcessTree: true)` — always kill the tree to catch child processes
- `Process.WaitForExit(5000)` — use a timeout to avoid hanging
- Dispose the `Process` object after it exits to release OS handles
- The `Process.Exited` event may fire on a threadpool thread — use thread-safe patterns
- `StandardOutput.BaseStream` and `StandardInput.BaseStream` give you raw streams for LspMessageReader/Writer
