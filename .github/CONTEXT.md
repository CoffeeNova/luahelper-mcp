# LuaHelper MCP Server — Project Context

## What it does

A .NET 10 application that wraps Tencent's `lualsp.exe` (a Go-based LSP server for Lua) and exposes Lua diagnostics through the Model Context Protocol (MCP). AI assistants (GitHub Copilot, Claude Desktop) can call tools like `check_lua_file` to get Lua warnings and errors.

## Key design principles

1. **Thin wrapper** — translates MCP calls to LSP protocol. No business logic beyond orchestration.
2. **Stateful LSP, stateless MCP** — `lualsp.exe` maintains project state; the MCP server manages it.
3. **Single responsibility** — each component has one job (protocol, process, cache, config).
4. **Resilience** — `lualsp.exe` crashes trigger auto-restart with exponential backoff (2s→4s→8s, max 3).
5. **AOT-compatible** — attribute-based MCP tool registration enables NativeAOT compilation.

## Technology stack

| Component | Technology |
|---|---|
| Language | C# 12 |
| Runtime | .NET 10 |
| MCP SDK | `ModelContextProtocol` (NuGet) |
| JSON | `System.Text.Json` |
| Logging | `Microsoft.Extensions.Logging` + Console (stderr) |
| Testing | NUnit + Shouldly + NSubstitute + AutoFixture |
| LSP Server | `lualsp.exe` (LuaHelper v0.2.29) |
| Formatter | CSharpier |

## Project structure

```
luahelper-mcp/
├── .github/                    # AI agent instructions (this is the source of truth)
│   ├── CONTEXT.md              # This file
│   ├── ARCHITECTURE.md         # Module design, state machine, data flow
│   ├── skills/                 # Domain-specific knowledge for agents
│   ├── agents/                 # Agent definitions
│   ├── prompts/                # Reusable prompt templates
│   ├── tools/                  # PowerShell utility scripts
│   └── docs/                   # Architecture docs, dev plan, research, audit
├── src/
│   ├── LuaHelperMcpServer/     # Main .NET 10 console app
│   │   ├── Program.cs          # Entry point + DI
│   │   ├── appsettings.json    # Server configuration
│   │   ├── Models/             # Data models
│   │   ├── Services/           # Core services (LspClient, ProcessManager, etc.)
│   │   ├── Tools/              # MCP tool definitions (Phase 1+)
│   │   └── Extensions/         # DI registration helpers
│   ├── LuaHelperMcpServer.Tests.Unit/        # Unit tests (no filesystem, no processes)
│   └── LuaHelperMcpServer.Tests.Integration/ # Integration tests (real lualsp.exe)
├── lualsp/                     # Bundled lualsp.exe binaries (gitignored)
├── vscode-extension/           # VS Code extension wrapper (Phase 4)
└── AGENTS.md                   # Entry point for AI agents
```

## Code conventions

- **Namespaces**: `LuaHelperMcpServer.Models`, `LuaHelperMcpServer.Services`, etc.
- **Interfaces**: Prefix `I` (e.g., `ILspClient`, `IProcessManager`)
- **Implementations**: Concrete class named after interface without `I` (e.g., `LspClient`, `ProcessManager`)
- **Constructor injection**: All dependencies via constructor, with `ArgumentNullException.ThrowIfNull`
- **Async**: All I/O is async (`async Task`), no blocking calls
- **Method naming**: Do NOT use the `Async` suffix unless a sync version with the same name exists in the class (e.g., `GetConfig`, `CreateDefaultConfig`, not `GetConfigAsync`)
- **Cancellation**: All async methods accept `CancellationToken`
- **Logging**: `ILogger<T>` via constructor injection, all output to stderr
- **Formatting**: CSharpier (run `csharpier format src`)
- **Empty strings**: use `string.Empty`, never the `""` literal (e.g., property initializers, `?? string.Empty`, `Replace(..., string.Empty)`)

## Key gotchas

### LSP protocol
- Messages use `Content-Length: N\r\n\r\n{json}` framing — same wire format as MCP
- `lualsp.exe` MUST be started with `-mode=1 -logflag=0` (LSP mode)
- `-mode=0` (cmd mode) produces no output — do not use
- `PluginPath` in `initializationOptions` must point to the directory containing `lualsp.exe` (not the exe itself)
- `IgnoreFileOrDir` and `IgnoreFileOrDirError` must include `.vscode/` to avoid noise

### Process management
- `lualsp.exe` is a Go binary (~10 MB), single-file, no dependencies
- Process crashes are detected via `Process.Exited` event
- Restart policy: max 3 attempts, exponential backoff 2s→4s→8s
- Stderr must be read in background to avoid deadlocks (process stdout buffer fills up)

### Threading
- Single `ReadLoopAsync` background task reads from lualsp.exe stdout
- Responses (with `id`) → resolve `TaskCompletionSource` in `_pendingRequests`
- Notifications (no `id`) → route to handlers (e.g., `publishDiagnostics` → cache)
- All writes to stdin go through `SemaphoreSlim(1,1)` in `LspMessageWriter`
- No `Console.WriteLine` — all logging goes to stderr via `ILogger`

### Testing
- Unit tests must NOT touch filesystem or spawn real processes
- Use `FakeLspServer` (anonymous pipes) + `MockProcessManager` for LspClient tests; `FakeProcessLauncher`/`FakeProcessHandle` for `ProcessManager` logic tests (via the `IProcessLauncher` seam)
- Use `NSubstitute` (+ `AutoFixture` with `AutoNSubstituteCustomization`) + `IFileReader` to mock file I/O
- All assertions use `Shouldly` (e.g. `x.ShouldBe(y)`, `text.ShouldContain(s, Case.Sensitive)`); never `Assert.That` constraint syntax
- **Coverage gate:** hand-written unit line coverage must stay **> 80 %** (enforced in CI)
- Integration tests use real `lualsp.exe` + the real `LuaHelperMcpServer` binary over real stdio; MCP layer is newline-delimited JSON-RPC via `McpStdioClient`, LSP layer is `Content-Length` framing
- **No `Assert.Ignore` in the integration project** — if a required binary is missing the test **fails** with a clear message (supersedes the old "skip gracefully" guidance; CI provisions binaries via `fetch-lualsp.ps1`)
- Golden/exact assertions: fixtures + `.expected.json` goldens are updated together when `lualsp.exe` is upgraded

## Environment variables

| Variable | Purpose | Default fallback |
|---|---|---|
| `LUAHELPER_LUALSP_PATH` | Path to lualsp.exe | `lualsp/win-x64/lualsp.exe` |
| `LUAHELPER_PLUGIN_PATH` | PluginPath for initializationOptions | Directory of lualsp.exe |
| `LUAHELPER_EXTENSION_PATH` | VS Code extension root (for tests) | `C:\Users\...\yinfei.luahelper-0.2.29` |
| `LUAHELPER_MCP_SERVER_PATH` | Server binary for MCP integration tests (`exe` or `dotnet;<dll>`) | Release `LuaHelperMcpServer.dll` in `bin` |

## Build & test commands

```powershell
# Build all projects
dotnet build

# Run unit tests only (fast, no external dependencies)
dotnet test src/LuaHelperMcpServer.Tests.Unit

# Run integration tests (requires lualsp.exe)
dotnet test src/LuaHelperMcpServer.Tests.Integration

# Run all tests
dotnet test src/LuaHelperMcpServer.Tests.Unit ; dotnet test src/LuaHelperMcpServer.Tests.Integration

# Format code
csharpier format src

# Run console app against a project
dotnet run --project src/LuaHelperMcpServer -- "E:\Repository\SomeLuaProject"
```
