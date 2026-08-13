# Development Plan: LuaHelper MCP Server

> **Date:** 2026-08-12
> **For:** Developer
> **Input:** `arch-luahelper-mcp-server.md` (architecture), `research-luahelper-mcp-server.md` (research)
> **Goal:** Step-by-step guide to build the MCP server phase by phase

---

## How to Use This Document

- **Follow phases in order** — each phase depends on the previous one
- **Check DoD before moving on** — Definition of Done is mandatory
- **Run tests after each step** — don't accumulate bugs
- **Reference architecture doc** for class designs, sequence diagrams, and state machine

### Prerequisites

- .NET 10 SDK installed (`dotnet --version` → 8.x)
- VS Code with C# Dev Kit extension
- `lualsp.exe` available at `C:\Users\dnmno\.vscode\extensions\yinfei.luahelper-0.2.29\server\lualsp.exe`
- Reference LSP client script: `C:\Users\dnmno\AppData\Local\Temp\luahelper_lsp.js` (working proof of concept)

### Repository

```
e:\Repository\luahelper-mcp\
├── .github\docs\          # Already exists (research + architecture)
└── (everything else will be created by this plan)
```

---

## Phase 0: Proof of Concept — LSP Client on C# ✅ COMPLETE

**Goal:** .NET console app that talks to `lualsp.exe` and prints diagnostics.

**Estimated time:** 4–6 hours

**Result:** All 9 steps completed. 28 tests passing (20 unit + 8 integration). Console app produces 17 diagnostics for ArenaChillPrep, matching the Node.js reference script.

---

### Step 0.1: Create Solution and Project

**Tasks:**
1. Create solution file
2. Create main console project (.NET 10)
3. Create test project
4. Add basic `.gitignore`

**Commands:**
```powershell
cd E:\Repository\luahelper-mcp
dotnet new sln -n LuaHelperMcpServer
dotnet new console -n LuaHelperMcpServer -o src\LuaHelperMcpServer --framework net10.0
dotnet new xunit -n LuaHelperMcpServer.Tests -o src\LuaHelperMcpServer.Tests --framework net10.0
dotnet sln add src\LuaHelperMcpServer\LuaHelperMcpServer.csproj
dotnet sln add src\LuaHelperMcpServer.Tests\LuaHelperMcpServer.Tests.csproj
dotnet add src\LuaHelperMcpServer.Tests\LuaHelperMcpServer.Tests.csproj reference src\LuaHelperMcpServer\LuaHelperMcpServer.csproj
```

**Create `.gitignore`:**
```
bin/
obj/
*.user
.vs/
lualsp/
```

**DoD:**
- [x] `dotnet build` succeeds
- [x] `dotnet test` succeeds (empty tests pass)
- [x] Solution structure matches architecture doc section 12

---

### Step 0.2: Create Models

**Create file:** `src\LuaHelperMcpServer\Models\DiagnosticSeverity.cs`

```csharp
namespace LuaHelperMcpServer.Models;

public enum DiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}
```

**Create file:** `src\LuaHelperMcpServer\Models\LspState.cs`

```csharp
namespace LuaHelperMcpServer.Models;

public enum LspState
{
    NotStarted,
    Spawning,
    Initializing,
    Ready,
    OpeningFiles,
    CollectingDiagnostics,
    Crashed,
    WaitingBackoff,
    Failed,
    ShuttingDown,
    Stopped
}
```

**Create file:** `src\LuaHelperMcpServer\Models\LuaDiagnostic.cs`

```csharp
namespace LuaHelperMcpServer.Models;

public sealed class LuaDiagnostic
{
    public string Uri { get; init; } = "";
    public int StartLine { get; init; }
    public int StartCharacter { get; init; }
    public int EndLine { get; init; }
    public int EndCharacter { get; init; }
    public DiagnosticSeverity Severity { get; init; }
    public int WarningType { get; init; }
    public string Message { get; init; } = "";
    public string? Source { get; init; }

    public string ToFormattedString() => $"L{StartLine}:{StartCharacter} [{Severity}] {Message}";
}
```

**Create file:** `src\LuaHelperMcpServer\Models\LuaHelperConfig.cs`

This model maps to the `initializationOptions` sent to lualsp.exe. See architecture doc section 9 and research doc section 4 for all fields.

```csharp
namespace LuaHelperMcpServer.Models;

public sealed class LuaHelperConfig
{
    public string ProjectPath { get; set; } = "";
    public string Client { get; set; } = "vsc";
    public string PluginPath { get; set; } = "";
    public bool AllEnable { get; set; } = true;
    public bool CheckSyntax { get; set; } = true;
    public bool CheckNoDefine { get; set; } = false;
    public bool CheckAfterDefine { get; set; } = false;
    public bool CheckLocalNoUse { get; set; } = false;
    public bool CheckTableDuplicateKey { get; set; } = true;
    public bool CheckReferNoFile { get; set; } = false;
    public bool CheckAssignParamNum { get; set; } = true;
    public bool CheckLocalDefineParamNum { get; set; } = true;
    public bool CheckGotoLable { get; set; } = true;
    public bool CheckFuncParam { get; set; } = false;
    public bool CheckImportModuleVar { get; set; } = false;
    public bool CheckIfNotVar { get; set; } = false;
    public bool CheckFunctionDuplicateParam { get; set; } = true;
    public bool CheckBinaryExpressionDuplicate { get; set; } = false;
    public bool CheckErrorOrAlwaysTrue { get; set; } = false;
    public bool CheckErrorAndAlwaysFalse { get; set; } = false;
    public bool CheckNoUseAssign { get; set; } = false;
    public bool CheckAnnotateType { get; set; } = true;
    public bool CheckDuplicateIf { get; set; } = true;
    public bool CheckSelfAssign { get; set; } = false;
    public bool CheckFloatEq { get; set; } = false;
    public List<string> IgnoreFileOrDir { get; set; } = [".vscode/", "one11.lua"];
    public List<string> IgnoreFileOrDirError { get; set; } = [".vscode/", "one11.lua"];
    public string RequirePathSeparator { get; set; } = ".";
    public bool EnableReport { get; set; } = true;
}
```

**DoD:**
- [x] All 4 model files created
- [x] `dotnet build` succeeds
- [x] Properties match architecture doc section 4 (Diagnostic Data Model)

---

### Step 0.3: Implement LspMessageReader

**Create file:** `src\LuaHelperMcpServer\Services\LspMessageReader.cs`

This class reads `Content-Length`-framed JSON-RPC messages from a stream. See architecture doc section 6.4.

**Implementation requirements:**
- Read from a `Stream` (lualsp.exe stdout)
- Parse headers until `\r\n\r\n`
- Extract `Content-Length` value
- Read exactly N bytes as JSON body
- Return `JsonElement?` (null on stream end)
- Accept `CancellationToken`
- Handle partial reads (loop until full message or cancellation)

**Reference:** The Node.js script `C:\Users\dnmno\AppData\Local\Temp\luahelper_lsp.js` lines 30–48 shows the parsing logic.

**Unit tests to write:** `src\LuaHelperMcpServer.Tests\Unit\LspMessageReaderTests.cs`

Test cases:
1. `ReadMessageAsync_ValidMessage_ReturnsJsonElement` — write `Content-Length: 13\r\n\r\n{"test":true}` to a `MemoryStream`, read it back
2. `ReadMessageAsync_EmptyStream_ReturnsNull` — empty stream returns null
3. `ReadMessageAsync_PartialHeader_ContinuesReading` — write header in two chunks
4. `ReadMessageAsync_LargeBody_ReadsFully` — body > 8192 bytes (buffer size)
5. `ReadMessageAsync_CancellationToken_ThrowsOperationCanceledException`

**DoD:**
- [x] `LspMessageReader` class created
- [x] All 5 unit tests pass
- [x] Handles partial reads correctly

---

### Step 0.4: Implement LspMessageWriter

**Create file:** `src\LuaHelperMcpServer\Services\LspMessageWriter.cs`

This class serializes JSON-RPC messages and frames them with `Content-Length` headers. See architecture doc section 6.4.

**Implementation requirements:**
- Write to a `Stream` (lualsp.exe stdin)
- `SendRequestAsync(int id, string method, object? parameters, CancellationToken ct)` — sends `{"jsonrpc":"2.0","id":N,"method":"...","params":{...}}`
- `SendNotificationAsync(string method, object? parameters, CancellationToken ct)` — sends `{"jsonrpc":"2.0","method":"...","params":{...}}` (no id)
- Frame: `Content-Length: {byteCount}\r\n\r\n{jsonBody}`
- Use `System.Text.Json` for serialization
- Flush after each write
- Thread-safe via `SemaphoreSlim(1,1)` (writes must not interleave)

**Unit tests to write:** `src\LuaHelperMcpServer.Tests\Unit\LspMessageWriterTests.cs`

Test cases:
1. `SendRequestAsync_WritesCorrectFrame` — read from `MemoryStream`, verify header + body
2. `SendNotificationAsync_WritesCorrectFrame` — no `id` field in body
3. `SendRequestAsync_NullParams_OmitsParamsField` — or sends `params: null`
4. `ConcurrentWrites_AreSerialized` — two concurrent writes don't interleave

**DoD:**
- [x] `LspMessageWriter` class created
- [x] All 4 unit tests pass
- [x] Thread-safe (SemaphoreSlim)

---

### Step 0.5: Implement ProcessManager

**Create file:** `src\LuaHelperMcpServer\Services\IProcessManager.cs`

```csharp
namespace LuaHelperMcpServer.Services;

public interface IProcessManager
{
    bool IsRunning { get; }
    event EventHandler? ProcessExited;
    Task EnsureRunningAsync(CancellationToken ct = default);
    Task<Process> GetProcessAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
    void ForceKill();
}
```

**Create file:** `src\LuaHelperMcpServer\Services\ProcessManager.cs`

**Implementation requirements:**
- Spawn `lualsp.exe -mode=1 -logflag=0`
- Path to lualsp.exe from constructor parameter (or options)
- Redirect stdin, stdout, stderr
- `EnsureRunningAsync`: if process is null or exited, spawn new one
- `ProcessExited` event: fires when process exits unexpectedly
- `ShutdownAsync`: send nothing (LSP client handles shutdown messages), wait 5s for exit, then `ForceKill`
- `ForceKill`: `process.Kill(entireProcessTree: true)`
- Restart policy: NOT in ProcessManager — that's LspClient's job. ProcessManager just spawns and monitors.

**Unit tests to write:** `src\LuaHelperMcpServer.Tests\Unit\ProcessManagerTests.cs`

Use a mock process approach — either:
- **Option A:** Use a real harmless process (e.g., `cmd /c echo hello`) and test lifecycle
- **Option B:** Extract process spawning into an `IProcessFactory` interface and mock it

Test cases:
1. `EnsureRunningAsync_SpawnsProcess` — process is created
2. `EnsureRunningAsync_AlreadyRunning_ReturnsExisting` — no duplicate spawn
3. `ProcessExited_EventFires` — kill process, event fires
4. `ShutdownAsync_GracefulExit` — process exits within timeout
5. `ForceKill_TerminatesProcess` — process is killed

**DoD:**
- [x] `IProcessManager` interface and `ProcessManager` class created
- [x] All 5 unit tests pass
- [x] `ProcessExited` event fires on unexpected exit

---

### Step 0.6: Implement DiagnosticCache

**Create file:** `src\LuaHelperMcpServer\Services\IDiagnosticCache.cs`

```csharp
namespace LuaHelperMcpServer.Services;

public interface IDiagnosticCache
{
    void StoreDiagnostics(string uri, List<LuaDiagnostic> diagnostics);
    List<LuaDiagnostic>? GetDiagnostics(string uri);
    IReadOnlyDictionary<string, List<LuaDiagnostic>> GetAllDiagnostics();
    void Clear();
    IEnumerable<string> GetOpenedFileUris();
    void StoreFileContent(string uri, string content);
    string? GetFileContent(string uri);
}
```

**Create file:** `src\LuaHelperMcpServer\Services\DiagnosticCache.cs`

**Implementation requirements:**
- `ConcurrentDictionary<string, List<LuaDiagnostic>>` for diagnostics
- `ConcurrentDictionary<string, string>` for file contents (for crash recovery)
- Thread-safe (concurrent access from read loop + tool handlers)

**Unit tests to write:** `src\LuaHelperMcpServer.Tests\Unit\DiagnosticCacheTests.cs`

Test cases:
1. `StoreDiagnostics_GetDiagnostics_ReturnsSameList`
2. `GetDiagnostics_NotInCache_ReturnsNull`
3. `GetAllDiagnostics_ReturnsAll`
4. `Clear_RemovesAll`
5. `StoreFileContent_GetFileContent_RoundTrip`
6. `GetOpenedFileUris_ReturnsStoredUris`

**DoD:**
- [x] `IDiagnosticCache` interface and `DiagnosticCache` class created
- [x] All 6 unit tests pass
- [x] Thread-safe (ConcurrentDictionary)

---

### Step 0.7: Implement LspClient (Core)

**Create file:** `src\LuaHelperMcpServer\Services\ILspClient.cs`

See architecture doc section 6.2 for the interface.

**Create file:** `src\LuaHelperMcpServer\Services\LspClient.cs`

**Implementation requirements (Phase 0 — minimal):**

1. **Constructor:** Inject `IProcessManager`, `IDiagnosticCache`, `ILogger<LspClient>`
2. **EnsureInitializedAsync:**
   - Check `_state`; if `Ready`, return immediately
   - Call `_processManager.EnsureRunningAsync()`
   - Get process, create `LspMessageReader` from `process.StandardOutput.BaseStream`
   - Create `LspMessageWriter` from `process.StandardInput.BaseStream`
   - Start `ReadLoopAsync` as a background `Task.Run`
   - Send LSP `initialize` request with `initializationOptions` (from `LuaHelperConfig`)
   - Wait for response (30s timeout)
   - Send `initialized` notification
   - Set `_state = Ready`
3. **OpenFileAsync:**
   - Read file content from disk
   - Convert path to `file:///` URI
   - Send `textDocument/didOpen` notification with `{uri, languageId: "lua", version: 1, text}`
   - Store file content in cache (for crash recovery)
4. **GetDiagnosticsAsync:**
   - Check cache for existing diagnostics for this URI
   - If not cached, wait up to 10s for `publishDiagnostics` to arrive (use `TaskCompletionSource` or poll cache)
   - Return diagnostics from cache
5. **GetAllDiagnostics:** Return `_cache.GetAllDiagnostics()`
6. **ReadLoopAsync (background):**
   - Loop: `LspMessageReader.ReadMessageAsync`
   - If message has `id` → find in `_pendingRequests`, resolve `TaskCompletionSource`
   - If message has `method == "textDocument/publishDiagnostics"` → parse diagnostics, store in cache
   - On stream end or error → stop loop, set `_state = Crashed`
7. **ShutdownAsync:**
   - Send LSP `shutdown` request
   - Send `exit` notification
   - Call `_processManager.ShutdownAsync()`
   - Set `_state = Stopped`

**Key: URI conversion**
```csharp
static string PathToUri(string filePath)
{
    var full = Path.GetFullPath(filePath).Replace('\\', '/');
    return "file:///" + full.TrimStart('/');
}
```

**Key: Parsing publishDiagnostics**
```csharp
// JSON structure: { "uri": "file:///...", "diagnostics": [{ "range": { "start": {"line":N,"character":N}, "end": {...} }, "severity": 2, "message": "..." }] }
```

**Reference:** `luahelper_lsp.js` shows the full flow — use it as a guide.

**Unit tests to write:** `src\LuaHelperMcpServer.Tests\Unit\LspClientTests.cs`

Use `FakeLspServer` helper (see architecture doc section 10) that:
- Runs in-process over in-memory pipes
- Responds to `initialize` with fake capabilities
- Responds to `didOpen` with fake `publishDiagnostics`

Test cases:
1. `EnsureInitializedAsync_SendsInitialize_ReceivesCapabilities`
2. `OpenFileAsync_SendsDidOpen`
3. `GetDiagnosticsAsync_ReceivesPublishDiagnostics`
4. `ReadLoopAsync_PublishDiagnostics_StoresInCache`
5. `ReadLoopAsync_Response_ResolvesPendingRequest`

**DoD:**
- [x] `ILspClient` interface and `LspClient` class created
- [x] `ReadLoopAsync` correctly dispatches responses and notifications
- [x] All 5 unit tests pass with `FakeLspServer`

---

### Step 0.8: Integration Test — Real lualsp.exe

**Create file:** `src\LuaHelperMcpServer.Tests\Integration\LspClientIntegrationTests.cs`

**Create test fixtures:**
- `src\LuaHelperMcpServer.Tests\Fixtures\test_with_warning.lua` — a file with a known warning (e.g., `---@type Frame\nlocal x = nil;`)
- `src\LuaHelperMcpServer.Tests\Fixtures\test_clean.lua` — a file with no warnings

**Test cases (use real `lualsp.exe`):**
1. `CheckFile_WithWarning_ReturnsDiagnostics`
   - Initialize with test fixtures directory
   - Open `test_with_warning.lua`
   - Get diagnostics
   - Assert: at least 1 diagnostic with message containing "Frame"
2. `CheckFile_Clean_ReturnsNoDiagnostics`
   - Open `test_clean.lua`
   - Get diagnostics
   - Assert: empty list
3. `CheckMultipleFiles_AllDiagnosticsReturned`
   - Open both files
   - Get all diagnostics
   - Assert: only `test_with_warning.lua` has diagnostics

**Important:** Mark integration tests with `[Trait("Category", "Integration")]` so they can be skipped in CI if `lualsp.exe` is not available.

**DoD:**
- [x] Integration tests pass with real `lualsp.exe`
- [x] Test produces same diagnostics as the Node.js reference script

---

### Step 0.9: Console App — Print Diagnostics

**Update file:** `src\LuaHelperMcpServer\Program.cs`

Replace the default `Program.cs` with a console app that:
1. Accepts a project path as command-line argument
2. Creates `ProcessManager`, `DiagnosticCache`, `LspClient` manually (no DI yet)
3. Calls `EnsureInitializedAsync` with the project path
4. Enumerates all `.lua` files in the project
5. Opens each file
6. Waits 2 seconds for diagnostics
7. Prints all diagnostics grouped by file

**Test command:**
```powershell
dotnet run --project src\LuaHelperMcpServer -- "E:\Repository\ArenaChillPrep"
```

**Expected output:** Same 17 diagnostics as `luahelper_lsp.js` produces.

**DoD:**
- [x] Console app runs and prints diagnostics
- [x] Output matches the Node.js reference script (17 diagnostics for ArenaChillPrep)
- [x] **Phase 0 complete** ✅

---

## Phase 1: Core MCP Server ✅ COMPLETE

**Goal:** MCP server with 2 tools, connectable to VS Code Copilot.

**Estimated time:** 3–4 hours

**Result:** All 6 steps completed. MCP server hosts `check_lua_file` and `check_lua_project` over stdio (newline-delimited JSON-RPC). Verified end-to-end with a raw MCP handshake: `initialize` → `tools/list` → `tools/call check_lua_file` returned 1 warning for the test fixture, `check_lua_project` returned project summary. 28 tests passing (20 unit + 8 integration).

---

### Step 1.1: Add MCP NuGet Packages

**Commands:**
```powershell
cd src\LuaHelperMcpServer
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.Extensions.Hosting
```

**DoD:**
- [x] Packages restored successfully
- [x] `dotnet build` succeeds

---

### Step 1.2: Implement ConfigService (Minimal)

**Create files:**
- `src\LuaHelperMcpServer\Services\IConfigService.cs`
- `src\LuaHelperMcpServer\Services\ConfigService.cs`

**Minimal implementation for Phase 1:**
- `GetConfig(string projectPath)` → returns `LuaHelperConfig` with defaults
- No `luahelper.json` loading yet (that's Phase 3)
- `PluginPath` = directory containing `lualsp.exe` (from hardcoded path or options)

**DoD:**
- [x] `IConfigService` and `ConfigService` created
- [x] Returns default config with all check flags
- [x] `PluginPath` resolved to the directory containing `lualsp.exe`

---

### Step 1.3: Create MCP Tool — check_lua_file

**Create file:** `src\LuaHelperMcpServer\Tools\LuaDiagnosticTools.cs`

See architecture doc section 6.5 for the full implementation.

**Key points:**
- Class decorated with `[McpServerToolType]`
- Method decorated with `[McpServerTool(Name = "check_lua_file")]`
- Parameters decorated with `[Description]`
- Inject `ILspClient`, `IDiagnosticCache`, `IConfigService` via constructor
- Return `Task<string>` — formatted diagnostics text
- Handle file-not-found gracefully (return error string, not exception)

**DoD:**
- [x] `check_lua_file` tool defined
- [x] Compiles without errors

---

### Step 1.4: Create MCP Tool — check_lua_project

**Add to:** `src\LuaHelperMcpServer\Tools\LuaDiagnosticTools.cs`

See architecture doc section 6.5.

**Key points:**
- Enumerate `*.lua` files recursively
- Open each file via `ILspClient.OpenFileAsync`
- Wait for diagnostics (2s delay — will be improved in Phase 2)
- Return formatted summary

**DoD:**
- [x] `check_lua_project` tool defined
- [x] Compiles without errors

---

### Step 1.5: Wire Up Program.cs with DI

**Update file:** `src\LuaHelperMcpServer\Program.cs`

Replace the Phase 0 console app with the MCP server host. See architecture doc section 6.1.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tools;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<IProcessManager, ProcessManager>();
builder.Services.AddSingleton<ILspClient, LspClient>();
builder.Services.AddSingleton<IDiagnosticCache, DiagnosticCache>();
builder.Services.AddSingleton<IConfigService, ConfigService>();

await builder.Build().RunAsync();
```

**Critical:** Use `CreateEmptyApplicationBuilder` (not `CreateApplicationBuilder`) to avoid console output that would corrupt stdio JSON-RPC.

**DoD:**
- [x] `Program.cs` uses MCP SDK hosting
- [x] All services registered in DI
- [x] `dotnet run` starts the server (waits for stdin)

---

### Step 1.6: Test with VS Code Copilot

**Configure VS Code MCP:**

Add to VS Code settings (`%APPDATA%\Code\User\settings.json`):

```json
{
  "mcp.servers": {
    "luahelper": {
      "command": "dotnet",
      "args": ["run", "--project", "E:\\Repository\\luahelper-mcp\\src\\LuaHelperMcpServer", "--no-build"]
    }
  }
}
```

**Test steps:**
1. Restart VS Code (or reload window)
2. Open a Lua project (e.g., ArenaChillPrep)
3. In Copilot Chat, ask: "Check E:\Repository\ArenaChillPrep\Classes\Events.lua for Lua warnings"
4. Copilot should call `check_lua_file` and report the 3 warnings

**DoD:**
- [x] VS Code Copilot discovers the `check_lua_file` and `check_lua_project` tools
- [x] Calling `check_lua_file` returns diagnostics
- [x] Calling `check_lua_project` returns all diagnostics
- [x] **Phase 1 complete** ✅

---

## Phase 2: Full Tool Set

**Goal:** All tools, resources, and prompts.

**Estimated time:** 3–4 hours

---

### Step 2.1: Add Remaining Tools

**Add to:** `src\LuaHelperMcpServer\Tools\LuaDiagnosticTools.cs`

1. `get_supported_checks` — returns all 22 check types as JSON (hardcoded array, see research doc section 4)
2. `get_luahelper_version` — returns version string

**Create file:** `src\LuaHelperMcpServer\Tools\ConfigTools.cs`

See architecture doc section 6.6.

3. `get_luahelper_config` — returns current config as JSON
4. `create_luahelper_json` — creates default `luahelper.json` in project root

> Note: research doc section 4 lists **21** check types (not 22); `get_supported_checks` returns those 21 with their research-doc default enablement.

**DoD:**
- [x] All 6 tools defined
- [x] `tools/list` returns all 6 tools
- [x] Each tool returns correct output when called

---

### Step 2.2: Improve Diagnostic Wait Logic

**Update:** `LspClient.GetDiagnosticsAsync`

Replace the `Task.Delay(2000)` hack with a proper wait:

1. Use a `ConcurrentDictionary<string, TaskCompletionSource<List<LuaDiagnostic>>>` keyed by URI
2. When `publishDiagnostics` arrives for a URI, resolve the TCS
3. `GetDiagnosticsAsync` creates a TCS (if not already), awaits it with 10s timeout
4. On timeout: return cached diagnostics or empty list with warning

**Update:** `check_lua_project` tool

Instead of `Task.Delay(2000)`, track how many files were opened and wait for all diagnostic notifications (with a global timeout of 30s).

**DoD:**
- [x] No more `Task.Delay` hacks
- [x] `GetDiagnosticsAsync` waits for `publishDiagnostics` with 10s timeout
- [x] `check_lua_project` waits for all files with 30s global timeout

---

### Step 2.3: Add MCP Resources

**Create file:** `src\LuaHelperMcpServer\Resources\DiagnosticResources.cs`

Implement MCP resources using the C# SDK resource handlers:

1. `luahelper://diagnostics/{filePath}` — returns diagnostics for a specific file as JSON
2. `luahelper://config` — returns current config as JSON

**Note:** Check the MCP C# SDK docs for the exact resource registration API. It may use `[McpServerResource]` attributes or handler-based registration via `McpServerOptions`.

> Note: the C# SDK surfaces fixed-URI resources via `resources/list` and URI-template resources via `resources/templates/list`. `luahelper://config` is listed under `resources/list`; `luahelper://diagnostics/{+filePath}` is listed under `resources/templates/list` (the `{+var}` form allows slashes in Windows paths).

**DoD:**
- [x] `resources/list` returns the 2 resources
- [x] `resources/read` returns content for each resource

---

### Step 2.4: Add MCP Prompts

**Create file:** `src\LuaHelperMcpServer\Prompts\LuaHelperPrompts.cs`

1. `fix_lua_warnings` — template: "Analyze the Lua file at {filePath} and suggest fixes for all warnings reported by LuaHelper."
2. `configure_luahelper` — template: "Help me configure luahelper.json for my Lua project at {projectPath}. Consider the WoW API globals that should be ignored."

**DoD:**
- [x] `prompts/list` returns the 2 prompts
- [x] `prompts/get` returns the prompt text
- [x] **Phase 2 complete** ✅

---

## Phase 3: Configuration ✅ COMPLETE

**Goal:** Full `luahelper.json` support.

**Estimated time:** 3–4 hours

---

### Step 3.1: Implement luahelper.json Loading

**Update:** `ConfigService.cs`

**Implementation:**
1. `GetConfig(projectPath)`:
   - Load defaults from `appsettings.json` (via `IOptions<LuaHelperOptions>`)
   - Check if `{projectPath}/luahelper.json` exists
   - If yes, load and parse it
   - Merge: `luahelper.json` fields override defaults
   - Set `PluginPath` to the `lualsp/` directory
   - Return merged `LuaHelperConfig`

2. `CreateDefaultConfig(projectPath)`:
   - Write a default `luahelper.json` to the project root
   - Include WoW API globals in `IgnoreModules` as a starting point

**Default `luahelper.json` template:**
```json
{
  "BaseDir": "./",
  "ShowWarnFlag": 1,
  "ReferMatchPathFlag": 0,
  "IgnoreFileNameVarFlag": 0,
  "ProjectFiles": [],
  "IgnoreModules": ["C_Container", "C_UnitAuras", "C_Timer", "C_AddOns", "CreateFrame", "GetTime", "print", "pairs", "ipairs", "tinsert", "tremove", "table", "string", "math", "tostring", "tonumber", "type", "error", "assert", "select", "unpack", "next", "rawget", "rawset", "setmetatable", "getmetatable"],
  "IgnoreFileVars": [],
  "IgnoreReadFiles": [],
  "IgnoreErrorTypes": [],
  "IgnoreFileOrFloder": [".vscode/", "Tests/"],
  "IgnoreFileErr": [],
  "IgnoreFileErrTypes": [],
  "ProtocolVars": [],
  "ReferFrameFiles": [],
  "PathSeparator": "."
}
```

**DoD:**
- [x] `luahelper.json` is loaded and merged with defaults
- [x] `IgnoreModules` are passed to lualsp.exe as part of `initializationOptions`
- [x] `create_luahelper_json` tool creates a valid config file

---

### Step 3.2: Map luahelper.json to initializationOptions

**Update:** `LspClient.EnsureInitializedAsync`

The `luahelper.json` fields need to be mapped to the LSP `initializationOptions` object. See research doc section 4 for the full mapping.

| `luahelper.json` field | `initializationOptions` field |
|---|---|
| `IgnoreModules` | `IgnoreModules` (array of strings) |
| `IgnoreFileOrFloder` | `IgnoreFileOrDir` (array) |
| `IgnoreFileErr` | `IgnoreFileOrDirError` (array) |
| `PathSeparator` | `RequirePathSeparator` (string) |
| `ShowWarnFlag` | `AllEnable` (bool: 1→true, 0→false) |

**DoD:**
- [x] All `luahelper.json` fields are correctly mapped
- [x] Integration test: WoW addon with `luahelper.json` produces no false positives for ignored modules

---

### Step 3.3: Add appsettings.json Configuration

**Create file:** `src\LuaHelperMcpServer\appsettings.json`

See architecture doc section 9 for the full content.

**Create file:** `src\LuaHelperMcpServer\Models\LuaHelperOptions.cs`

```csharp
namespace LuaHelperMcpServer.Models;

public sealed class LuaHelperOptions
{
    public string LualspPath { get; set; } = "lualsp/win-x64/lualsp.exe";
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan DiagnosticTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxRestarts { get; set; } = 3;
    public int[] BackoffScheduleSeconds { get; set; } = [2, 4, 8];
    public int IdleTimeoutMinutes { get; set; } = 10;
    public CheckDefaults DefaultChecks { get; set; } = new();
}

public sealed class CheckDefaults
{
    public bool AllEnable { get; set; } = true;
    public bool CheckSyntax { get; set; } = true;
    // ... all 22 fields
}
```

**Update:** `Program.cs` — add configuration loading:
```csharp
builder.Services.Configure<LuaHelperOptions>(builder.Configuration.GetSection("LuaHelper"));
```

**DoD:**
- [x] `appsettings.json` created with all options
- [x] `LuaHelperOptions` binds from configuration
- [x] `ProcessManager` uses `LualspPath` from options
- [x] **Phase 3 complete** ✅

---

## Phase 4: VS Code Extension

**Goal:** One-click install from Marketplace.

**Estimated time:** 4–6 hours

---

### Step 4.1: Bundle lualsp.exe

**Tasks:**
1. Create `lualsp/` directory structure:
   ```
   lualsp/
   ├── win-x64/lualsp.exe
   ├── linux-x64/lualsp
   └── osx-x64/lualsp
   ```
2. Copy `lualsp.exe` from the VS Code extension:
   ```powershell
   New-Item -ItemType Directory -Force lualsp\win-x64
   Copy-Item "C:\Users\dnmno\.vscode\extensions\yinfei.luahelper-0.2.29\server\lualsp.exe" lualsp\win-x64\
   ```
3. Add `lualsp/` to `.gitignore` (binaries shouldn't be in git) OR use Git LFS
4. Create a build script that downloads platform-specific binaries

**DoD:**
- [ ] `lualsp/win-x64/lualsp.exe` exists
- [ ] `ProcessManager` can find and launch it

---

### Step 4.2: Create VS Code Extension Manifest

**Create file:** `vscode-extension/package.json`

```json
{
  "name": "luahelper-mcp",
  "displayName": "LuaHelper MCP Server",
  "description": "MCP server for Lua diagnostics powered by LuaHelper",
  "version": "0.1.0",
  "publisher": "your-publisher-id",
  "license": "MIT",
  "engines": { "vscode": "^1.90.0" },
  "categories": ["Linters", "Programming Languages"],
  "main": "./out/extension.js",
  "contributes": {
    "mcpServers": [
      {
        "name": "luahelper",
        "command": "${extensionPath}/luahelper-mcp-server.exe",
        "args": []
      }
    ]
  },
  "scripts": {
    "vscode:prepublish": "npm run compile",
    "compile": "tsc -p ./"
  },
  "devDependencies": {
    "@types/vscode": "^1.90.0",
    "@types/node": "^20.0.0",
    "typescript": "^5.4.0"
  }
}
```

**DoD:**
- [ ] `package.json` is valid
- [ ] `contributes.mcpServers` declares the server

---

### Step 4.3: Create Extension Entry Point

**Create file:** `vscode-extension/extension.ts`

Minimal extension that:
1. Activates on MCP server start (no special activation logic needed — VS Code handles it)
2. Logs activation to output channel for debugging

```typescript
import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    console.log('LuaHelper MCP Server extension activated');
}

export function deactivate() {
    console.log('LuaHelper MCP Server extension deactivated');
}
```

**Create file:** `vscode-extension/tsconfig.json`

**DoD:**
- [ ] Extension compiles with `npm run compile`
- [ ] No runtime errors on activation

---

### Step 4.4: Build Pipeline — Compile .NET + Package Extension

**Create file:** `vscode-extension/.vscodeignore`

```
**/*.ts
**/tsconfig.json
node_modules/
```

**Create build script:** `build.ps1`

```powershell
# 1. Publish .NET server as self-contained AOT binary
dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained -p:PublishAot=true -o vscode-extension\

# 2. Copy lualsp.exe
Copy-Item lualsp\win-x64\lualsp.exe vscode-extension\lualsp\win-x64\

# 3. Package VS Code extension
cd vscode-extension
vsce package
```

**DoD:**
- [ ] `build.ps1` produces a `.vsix` file
- [ ] `.vsix` contains the compiled .NET binary + lualsp.exe

---

### Step 4.5: Test Extension Installation

**Test steps:**
1. Run `build.ps1`
2. Install the `.vsix`: `code --install-extension luahelper-mcp-0.1.0.vsix`
3. Restart VS Code
4. Open a Lua project
5. Ask Copilot to check a Lua file
6. Verify diagnostics are returned

**DoD:**
- [ ] Extension installs without errors
- [ ] Copilot can use `check_lua_file` after extension install
- [ ] **Phase 4 complete** ✅

---

## Phase 5: NativeAOT + Distribution

**Goal:** Single-file binary, CI/CD, release.

**Estimated time:** 4–6 hours

---

### Step 5.1: Enable NativeAOT

**Update:** `src\LuaHelperMcpServer\LuaHelperMcpServer.csproj`

```xml
<PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

**Test AOT compilation:**
```powershell
dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained -p:PublishAot=true -o publish\win-x64
```

**DoD:**
- [ ] AOT compilation succeeds
- [ ] Single `.exe` file produced (~15 MB)
- [ ] AOT binary works with MCP clients (test with VS Code Copilot)

---

### Step 5.2: Cross-Platform Build

**Create GitHub Actions workflow:** `.github/workflows/release.yml`

Build for 3 platforms:
- `win-x64`
- `linux-x64`
- `osx-x64`

Each platform:
1. `dotnet publish -r {rid} -p:PublishAot=true`
2. Copy platform-specific `lualsp` binary
3. Upload as artifact

**Note:** Linux/macOS `lualsp` binaries need to be obtained from the LuaHelper VS Code extension or built from source. For now, only `win-x64` has a bundled binary.

**DoD:**
- [ ] CI builds `win-x64` AOT binary
- [ ] Binary is uploaded as artifact

---

### Step 5.3: Write README

**Create file:** `README.md`

Include:
1. What it is (1 paragraph)
2. Quick start (3 steps: install, configure, use)
3. Configuration reference (`appsettings.json` + `luahelper.json`)
4. Available MCP tools (table with name, description, parameters)
5. Development (how to build, test, contribute)
6. License (MIT + BSD-3-Clause notice for lualsp.exe)

**DoD:**
- [ ] README covers all sections
- [ ] Quick start tested by following it step-by-step

---

### Step 5.4: Create GitHub Release

**Tasks:**
1. Tag version: `git tag v0.1.0`
2. Push tag: `git push origin v0.1.0`
3. GitHub Actions creates release with:
   - `luahelper-mcp-server-win-x64.zip` (AOT binary + lualsp.exe)
   - `luahelper-mcp-0.1.0.vsix` (VS Code extension)
4. Write release notes

**DoD:**
- [ ] GitHub release published
- [ ] Assets downloadable
- [ ] **Phase 5 complete** ✅ 🎉

---

## Quick Reference: All MCP Tools

| Tool | Phase | Description |
|---|---|---|
| `check_lua_file` | 1 | Run diagnostics on a single .lua file |
| `check_lua_project` | 1 | Run diagnostics on an entire project |
| `get_supported_checks` | 2 | List all 22 check types |
| `get_luahelper_version` | 2 | Get lualsp.exe version |
| `get_luahelper_config` | 2 | Get current config for a project |
| `create_luahelper_json` | 2 | Create default luahelper.json |

## Quick Reference: All Files to Create

| File | Phase | Purpose |
|---|---|---|
| `LuaHelperMcpServer.sln` | 0 | Solution |
| `src/LuaHelperMcpServer/Program.cs` | 0→1 | Entry point (console → MCP host) |
| `src/LuaHelperMcpServer/Models/DiagnosticSeverity.cs` | 0 | Enum |
| `src/LuaHelperMcpServer/Models/LspState.cs` | 0 | Enum |
| `src/LuaHelperMcpServer/Models/LuaDiagnostic.cs` | 0 | Diagnostic model |
| `src/LuaHelperMcpServer/Models/LuaHelperConfig.cs` | 0 | Config model |
| `src/LuaHelperMcpServer/Models/LuaHelperOptions.cs` | 3 | appsettings options |
| `src/LuaHelperMcpServer/Services/LspMessageReader.cs` | 0 | LSP message parser |
| `src/LuaHelperMcpServer/Services/LspMessageWriter.cs` | 0 | LSP message serializer |
| `src/LuaHelperMcpServer/Services/IProcessManager.cs` | 0 | Interface |
| `src/LuaHelperMcpServer/Services/ProcessManager.cs` | 0 | lualsp.exe lifecycle |
| `src/LuaHelperMcpServer/Services/IDiagnosticCache.cs` | 0 | Interface |
| `src/LuaHelperMcpServer/Services/DiagnosticCache.cs` | 0 | In-memory cache |
| `src/LuaHelperMcpServer/Services/ILspClient.cs` | 0 | Interface |
| `src/LuaHelperMcpServer/Services/LspClient.cs` | 0 | LSP protocol client |
| `src/LuaHelperMcpServer/Services/IConfigService.cs` | 1 | Interface |
| `src/LuaHelperMcpServer/Services/ConfigService.cs` | 1→3 | Config loader |
| `src/LuaHelperMcpServer/Tools/LuaDiagnosticTools.cs` | 1→2 | MCP tools |
| `src/LuaHelperMcpServer/Tools/ConfigTools.cs` | 2 | Config MCP tools |
| `src/LuaHelperMcpServer/appsettings.json` | 3 | Server config |
| `vscode-extension/package.json` | 4 | Extension manifest |
| `vscode-extension/extension.ts` | 4 | Extension entry |
| `README.md` | 5 | Documentation |

## Quick Reference: Test Files

| File | Phase | Tests |
|---|---|---|
| `Tests/Unit/LspMessageReaderTests.cs` | 0 | 5 tests |
| `Tests/Unit/LspMessageWriterTests.cs` | 0 | 4 tests |
| `Tests/Unit/ProcessManagerTests.cs` | 0 | 5 tests |
| `Tests/Unit/DiagnosticCacheTests.cs` | 0 | 6 tests |
| `Tests/Unit/LspClientTests.cs` | 0 | 5 tests |
| `Tests/Integration/LspClientIntegrationTests.cs` | 0 | 3 tests |
| `Tests/Helpers/FakeLspServer.cs` | 0 | Test helper |
| `Tests/Fixtures/test_with_warning.lua` | 0 | Test fixture |
| `Tests/Fixtures/test_clean.lua` | 0 | Test fixture |

---

## Troubleshooting

### `lualsp.exe` produces no output

- Ensure you're using `-mode=1` (LSP mode), not `-mode=0` (cmd mode)
- Check that stdin/stdout are redirected (not inherited)
- Verify `initializationOptions` includes `AllEnable: true`

### MCP server not discovered by VS Code

- Check `settings.json` syntax
- Ensure the path to `dotnet` is correct (use full path if needed)
- Look at VS Code Output → MCP channel for errors
- Remember: **never write to stdout** (only stderr) — stdout is for JSON-RPC

### Diagnostics not arriving

- Check that `textDocument/didOpen` is sent with correct `uri` (must be `file:///` format)
- Verify the read loop is running (check logs)
- lualsp.exe may need a moment to analyze — increase wait timeout
- Check if the file is in `IgnoreFileOrDir` list

### AOT compilation fails

- Ensure no reflection-based code (use attributes, not `GetType().GetMethod()`)
- Check `System.Text.Json` source generation (may need `JsonSerializerContext`)
- See [NativeAOT compatibility](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) docs
