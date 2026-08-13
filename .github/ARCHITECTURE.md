# LuaHelper MCP Server — Architecture

> **Full architecture document:** `.github/docs/arch-luahelper-mcp-server.md`
> **This file** is a concise reference for AI agents. Read the full doc for sequence diagrams, state machines, and detailed component design.

## High-level architecture

```
AI Assistant → MCP Server (stdio) → LspClient → lualsp.exe (LSP stdin/stdout)
                                        ↕
                                  DiagnosticCache (in-memory)
                                        ↕
                                  ConfigService (luahelper.json + appsettings.json)
```

## Component responsibilities

| Component | File | Job |
|---|---|---|
| `Program.cs` | `src/LuaHelperMcpServer/Program.cs` | DI setup, stdio transport, entry point |
| `LspMessageReader` | `Services/LspMessageReader.cs` | Parse `Content-Length`-framed JSON-RPC from stream |
| `LspMessageWriter` | `Services/LspMessageWriter.cs` | Serialize JSON-RPC with `Content-Length` headers (thread-safe) |
| `ProcessManager` | `Services/ProcessManager.cs` | Spawn, monitor, restart (3 attempts, 2s→4s→8s backoff), kill |
| `DiagnosticCache` | `Services/DiagnosticCache.cs` | Thread-safe in-memory store (ConcurrentDictionary) |
| `LspClient` | `Services/LspClient.cs` | LSP protocol: initialize, didOpen, receive diagnostics, shutdown |
| `ConfigService` | `Services/ConfigService.cs` | Load/merge luahelper.json + appsettings.json |
| `FileReader` | `Services/FileReader.cs` | File I/O abstraction (for testability) |

## LSP state machine

```
NotStarted → Spawning → Initializing → Ready → (didOpen → Ready) → ShuttingDown → Stopped
                ↓            ↓                                    ↓
             Failed       Failed                              Crashed → WaitingBackoff → Spawning
```

## Threading model

- **Main thread**: MCP stdio transport (hosted by `Host.RunAsync`)
- **Read loop** (`ThreadPool`): `ReadLoopAsync` reads lualsp.exe stdout, dispatches responses and notifications
- **Process monitor** (`ThreadPool`): `ReadStderrAsync` reads stderr to prevent deadlocks
- **Concurrency primitives**: `SemaphoreSlim(1,1)` for writes, `ConcurrentDictionary` for pending requests, `CancellationTokenSource` for lifecycle

## Error handling

| Error | Strategy |
|---|---|
| lualsp.exe not found | Return tool error with path + install instructions |
| lualsp.exe spawn fails | Retry with backoff (3 attempts), then return error |
| lualsp.exe crashes mid-operation | Auto-restart, re-open cached files, retry once |
| LSP initialize timeout (30s) | Kill process, respawn, retry once |
| publishDiagnostics timeout (10s) | Return cached diagnostics (if any) or empty result |
| File not found | Return tool error |
| Invalid JSON from lualsp.exe | Log warning, skip message, continue read loop |

## Key interfaces

```
IProcessManager → ProcessManager
ILspClient → LspClient
IDiagnosticCache → DiagnosticCache
IConfigService → ConfigService
IFileReader → FileReader
```

## Data model

```
LuaDiagnostic { Uri, StartLine, StartCharacter, EndLine, EndCharacter, Severity, WarningType, Message }
DiagnosticCollection { ProjectPath, ByFile (Dictionary), Timestamp, TotalCount }
LuaHelperConfig { ProjectPath, PluginPath, AllEnable, CheckSyntax, ...22 check flags... }
LuaHelperOptions { LualspPath, DefaultTimeout, DiagnosticTimeout, MaxRestarts, BackoffScheduleSeconds }
InitializeOptions { Client, PluginPath, AllEnable, ...22 check flags..., IgnoreFileOrDir, RequirePathSeparator }
```