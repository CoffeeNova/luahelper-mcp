# Test Plan — LuaHelper MCP Server

Sub-plan of `dev-plan-luahelper-mcp-server.md`. Single source of truth for the
test-coverage work. Update this file **before** writing tests (contract-first).

## 1. Goals & hard constraints

| # | Goal | Acceptance |
|---|---|---|
| G1 | Unit-test line coverage of hand-written code **> 80 %** | `dotnet test src/LuaHelperMcpServer.Tests.Unit --collect:"XPlat Code Coverage"` reports line-rate > 0.80 on non-generated source |
| G2 | Integration tests cover **every MCP tool scenario** (all 7 tools, 2 resources, 2 prompts) end-to-end through the real server binary | All tests in `LuaHelperMcpServer.Tests.Integration` green in CI |
| G3 | Integration tests drive the **real executables** over real stdin/stdout | LSP layer → real `lualsp.exe`; MCP layer → real `LuaHelperMcpServer` process spawned with redirected stdio |
| G4 | **No graceful skipping.** Integration tests must **fail** (not `Assert.Ignore`) when `lualsp.exe`/server binary is missing | No `Assert.Ignore` calls in the integration project |
| G5 | When `lualsp.exe` is upgraded, **tests (fixtures + expected values) are updated** to match — golden/exact assertions, not fuzzy tolerances | Each integration test asserts exact expected output |
| G6 | CI provisions `lualsp.exe` and the server binary, then runs the full integration suite | `ci.yml` integration step runs `dotnet test ...Tests.Integration` against a provisioned `lualsp.exe` |
| G7 | Before any test work, refactor the code **analyzer-driven**: delete dead code and fix code-analyser warnings, using **only** findings from the dotnet-debugger MCP server | §4 gate: per-project ReSharper inspection (`severity=warning`, `noBuild=true`) clean; every deletion/fix traceable to an analyzer finding; no self-directed refactoring |

### Overridden guidance

This plan **supersedes** earlier guidance that said "integration tests skip
gracefully with `Assert.Ignore` if `lualsp.exe` not found" (in
`nunit-testing/SKILL.md`, `CONTEXT.md`, and the original dev-plan Step 0.8).
Rationale: skipping hides regressions and lets CI pass without actually
running the integration suite. CI must provision the binary; locally the
developer runs `fetch-lualsp.ps1` first. The `nunit-testing` skill and
`CONTEXT.md` "Testing" section must be updated to match (see §9).

## 2. Current state (measured)

Coverage run: `dotnet test src/LuaHelperMcpServer.Tests.Unit --collect:"XPlat Code Coverage"`,
35 tests passing. Hand-written (non-generated) source only:

| File | Covered / Total | % |
|---|---|---|
| `Models/InitializeOptions.cs` | 0 / 29 | 0 % — **DEAD CODE** (declared, never referenced; `BuildInitializationOptions` uses `JsonObject` instead) |
| `Services/ProcessManager.cs` | 0 / 161 | 0 % — tested only in integration with real `cmd.exe`; not unit-testable as-is |
| `Program.cs` | 0 / 21 | 0 % — entry point |
| `Resources/DiagnosticResources.cs` | 0 / 49 | 0 % |
| `Extensions/ServiceCollectionExtensions.cs` | 0 / 24 | 0 % |
| `Models/LuahelperJsonTemplate.cs` | 0 / 65 | 0 % |
| `Models/DiagnosticCollection.cs` | 0 / 20 | 0 % |
| `Prompts/LuaHelperPrompts.cs` | 0 / 8 | 0 % |
| `Services/FileReader.cs` | 0 / 2 | 0 % |
| `Extensions/LualspPathResolver.cs` | 4 / 12 | 33 % |
| `Tools/LuaDiagnosticTools.cs` | 147 / 226 | 65 % |
| `Models/LuaDiagnostic.cs` | 8 / 10 | 80 % |
| `Services/ConfigService.cs` | 179 / 214 | 84 % |
| `Services/LspClient.cs` | 267 / 303 | 88 % |
| `Services/LspMessageReader.cs` | 59 / 65 | 91 % |
| `Tools/ConfigTools.cs` | 14 / 15 | 93 % |
| `Tools/VersionTools.cs` | 9 / 9 | 100 % |
| `Services/LspMessageWriter.cs` | 44 / 44 | 100 % |
| `Services/DiagnosticCache.cs` | 24 / 24 | 100 % |
| `Serialization/LspJsonContext.cs` | 11 / 11 | 100 % |
| `Models/LuaHelperOptions.cs` | 29 / 29 | 100 % |
| `Models/LuaHelperConfig.cs` | 30 / 30 | 100 % |
| `Models/SupportedCheck.cs` | 3 / 3 | 100 % |
| **Total (hand-written)** | **828 / 1374** | **60.3 %** |

Existing test files:

- Unit (35 tests): `LspMessageReaderTests`, `LspMessageWriterTests`,
  `DiagnosticCacheTests`, `ConfigServiceTests`, `LspClientTests`,
  `LuaDiagnosticToolsTests`, `ConfigToolsTests`, `VersionToolsTests`.
  Helpers: `FakeLspServer` (anonymous pipes), `MockProcessManager`,
  `PartialWriteStream`.
- Integration (11 tests): `LspClientIntegrationTests` (6, real `lualsp.exe`,
  fixtures `test_with_warning.lua` / `test_clean.lua`, ignore-modules
  scenarios), `ProcessManagerTests` (5, real `cmd.exe`).

### Coverage gap analysis

To exceed 80 % of 1374 lines we need **≥ 1100 lines covered** (currently 828 →
+272 required). The gaps, in priority order:

1. `ProcessManager.cs` (161 lines @ 0 %) — **blocked** by "no real processes"
   unit rule. Requires a testability seam (§4). Unblocking this contributes the
   single largest gain (~+137 lines at 85 %).
2. `LuaDiagnosticTools.cs` (79 uncovered) — `CheckLuaFile`, `CheckLuaProject`,
   crash-recovery, ignore-set helpers. Pure logic, easy to unit test (~+68).
3. `DiagnosticResources.cs` (49 @ 0 %), `DiagnosticCollection.cs` (20 @ 0 %),
  `LuahelperJsonTemplate.cs` (65 @ 0 %), `LuaHelperPrompts.cs` (8 @ 0 %),
  `ServiceCollectionExtensions.cs` (24 @ 0 %), `LualspPathResolver.cs` (+8),
  `LuaDiagnostic.cs` (+2), `LspMessageReader.cs` (+6), `LspClient.cs` (+21),
  `ConfigTools.cs` (+1) — all unit-testable (~+135 combined).
4. `ConfigService.cs` (35 uncovered) — only `CreateDefaultConfig` + `GetVersion`
   success path remain; both touch the real filesystem/exe → covered by
   **integration** (§6).
5. `Program.cs` (21 @ 0 %) — entry point; covered by the MCP-layer integration
   suite spawning the real binary (§6).

**Projected unit coverage after §4–§5:** ~860–880 / ~1345 ≈ **85 %** (after
deleting dead `InitializeOptions.cs` and adding the `IProcessLauncher` seam).

## 3. Code & tooling inventory (what must be covered)

### 3.1 Production code units

| Unit | Responsibility | Test layer |
|---|---|---|
| `LspMessageReader` | `Content-Length` framing, partial reads, malformed headers | Unit |
| `LspMessageWriter` | Request/notification framing, concurrent-write lock | Unit |
| `DiagnosticCache` | Store/retrieve diagnostics + file contents | Unit |
| `ConfigService` | Default config, `luahelper.json` merge (old+new names), `GetVersion`, `CreateDefaultConfig` | Unit (merge) + Integration (FS/exe) |
| `ProcessManager` | Spawn, restart counter, backoff, max-restart throw, graceful shutdown, force-kill, stderr drain | Unit (via seam) + Integration (real cmd.exe) |
| `LspClient` | Initialize, didOpen, diagnostics wait, shutdown, read-loop dispatch, crash state | Unit (FakeLspServer) + Integration (real lualsp.exe) |
| `LuaDiagnosticTools` | `check_lua_file`, `check_lua_project`, `get_supported_checks`, `get_luahelper_version`, crash recovery, ignore-set | Unit + Integration (MCP layer) |
| `ConfigTools` | `get_luahelper_config`, `create_luahelper_json` | Unit + Integration (MCP layer) |
| `VersionTools` | `get_server_version` | Unit + Integration (MCP layer) |
| `DiagnosticResources` | `luahelper://diagnostics/{+filePath}`, `luahelper://config` | Unit + Integration (MCP layer) |
| `LuaHelperPrompts` | `fix_lua_warnings`, `configure_luahelper` | Unit + Integration (MCP layer) |
| `DiagnosticCollection` | `ToFormattedString` project summary | Unit |
| `LuahelperJsonTemplate` | Default `luahelper.json` model | Unit (serialization) |
| `LualspPathResolver` | Default path per OS, resolve null/relative/rooted | Unit |
| `ServiceCollectionExtensions` | DI registration of all services | Unit (DI verification) |
| `FileReader` | Thin `File.*` wrapper | Integration (real FS) |
| `Program.cs` | Entry point + DI composition | Integration (spawn binary) |
| `LspJsonContext` | `System.Text.Json` source-gen context | Covered indirectly |

### 3.2 MCP surface (must all be exercised end-to-end in integration)

**Tools (7):** `get_server_version`, `get_luahelper_version`,
`get_supported_checks`, `get_luahelper_config`, `create_luahelper_json`,
`check_lua_file`, `check_lua_project`.

**Resources (2):** `luahelper://diagnostics/{+filePath}`, `luahelper://config`.

**Prompts (2):** `fix_lua_warnings`, `configure_luahelper`.

**Capability discovery:** `tools/list`, `resources/list`, `prompts/list`.

### 3.3 Tooling that must be driven by tests

| Tool | Driven by | Layer |
|---|---|---|
| `lualsp.exe` (real) | LSP-layer integration + MCP-layer integration (server spawns it internally) | Integration |
| `LuaHelperMcpServer` binary (real) | MCP-layer integration (spawned process, newline JSON-RPC over stdio) | Integration |
| `fetch-lualsp.ps1` | CI (provisions `lualsp/win-x64/lualsp.exe` before tests) | CI |
| `build.ps1` / `dotnet build` | CI (produces server dll/exe) | CI |

## 4. Pre-test refactoring — analyzer-driven (mandatory gate)

### 4.1 Analyzer workflow (dotnet-debugger MCP)

The repository contains only `LuaHelperMcpServer.slnx`. The dotnet-debugger MCP
does **not** support `.slnx` files — analyse **projects**, never the solution.

1. Run JetBrains ReSharper inspections on all three projects with
   `noBuild=true`, `severity=warning`, `timeoutSeconds=1500`:
   - `dotnet-debugger_resharper_inspect_project` with
     `projectPath=C:\Repository\luahelper-mcp\src\LuaHelperMcpServer\LuaHelperMcpServer.csproj`
   - `projectPath=C:\Repository\luahelper-mcp\src\LuaHelperMcpServer.Tests.Unit\LuaHelperMcpServer.Tests.Unit.csproj`
   - `projectPath=C:\Repository\luahelper-mcp\src\LuaHelperMcpServer.Tests.Integration\LuaHelperMcpServer.Tests.Integration.csproj`
2. Fix **only** what the analyzer reports: dead code (unused types/members) and
   warnings. **Self-directed refactoring is forbidden** — no renames, no
   restructures, no speculative "improvements". Every change must be traceable
   to a finding returned by dotnet-debugger.
3. Before deleting any dead-code candidate, confirm zero usages with
   `dotnet-debugger_code_find_usages` — delete only when the analyzer data
   confirms the symbol is unused.
4. After each fix batch, re-run the inspection for the affected project;
   iterate until it is warning-free. Remaining unfixable warnings must be
   recorded in this document with a reason.
5. The **only** non-analyzer refactor permitted is the plan-mandated
   testability seam in §4.3 — it is required to reach the >80 % unit-coverage
   target, and every change it makes is specified here in advance.

### 4.2 Delete dead code (analyzer-confirmed)

`Models/InitializeOptions.cs` (29 lines @ 0 %) is never referenced
(`LspClient.BuildInitializationOptions` builds a `JsonObject` directly).
Expected to be reported by ReSharper as an unused type — verify with
`dotnet-debugger_code_find_usages` (0 hits), then delete. Any **other** dead
code reported by the analyzer is deleted the same way. Deleting this file
removes 29 lines from the coverage denominator.

#### Step 2 outcome (2026-08-14) — analyzer gate executed

ReSharper 2026.1.2 inspections run per project (`--no-build`, `severity=warning`).
**Note:** the dotnet-debugger MCP's `resharper_inspect_project` started failing
with `MSB4237 NuGetSdkResolver` / `MSB1013 MSBuild.rsp` errors mid-session
(environment/toolset issue, not repo code). The identical analysis was re-run
via the engine's own CLI (`jb.exe inspectcode --no-build`) with the same
parameters; results below come from the SARIF reports.

| Project | Findings | Fixed | Remaining |
|---|---|---|---|
| `LuaHelperMcpServer` | 36 | 36 | 0 |
| `LuaHelperMcpServer.Tests.Unit` | 3 | 3 | 0 |
| `LuaHelperMcpServer.Tests.Integration` | 4 | 4 | 0 |

**Main project (36):** 13 × `RedundantDefaultMemberInitializer` (`= false`
bool / `= 0` int defaults removed in `LuaHelperConfig`, `CheckDefaults`,
`LuahelperJsonTemplate`), 3 × `RedundantUsingDirective` (`Program.cs`,
`LspJsonContext.cs`, `LspMessageWriter.cs`), 1 ×
`ConditionalAccessQualifierIsNonNullable` (`ServiceCollectionExtensions`
`BackoffScheduleSeconds?.Select` → `.Select`), 1 ×
`NullCoalescingConditionIsAlwaysNotNull` (`ConfigService` `?? new CheckDefaults()`
removed), 3 × `NotAccessedField.Local` (`LspClient._config`, `LspClient._readLoopTask`,
`LuaDiagnosticTools._cache` — fields + their ctor parameter/assignment removed;
`Task.Run` now discarded with `_ =`; `LuaDiagnosticTools` ctor is now
`(ILspClient, IConfigService)`, test call site updated).

**Unit project (3):** `FakeLspServer._runTask` never read (removed, `_ = Task.Run`),
`LspMessageReaderTests` captured-variable-disposed-in-outer-scope (restructured
to pass the `Task` to `Should.ThrowAsync<T>(task)`).

**Integration project (4):** 2 × redundant `?.` on non-nullable fields in
`TearDown`, 2 × redundant `Case.Insensitive` argument in `ShouldContain`
(default is case-insensitive).

**Dead code:** `Models/InitializeOptions.cs` deleted. ReSharper did **not**
report it (public type — unused-type inspection only flags non-public), but
`dotnet-debugger_code_find_usages` returned 0 usages (declaration only), which
satisfies the plan's verification; deletion proceeded per §4.2.

Unfixable findings: **none.** Gate is clean; `dotnet build` 0 warnings; all 35
unit tests green after the gate.

#### Step 3 outcome (2026-08-14) — testability seam

- New `Services/IProcessLauncher.cs`, `Services/IProcessHandle.cs`,
  `Services/ProcessLauncher.cs`, `Services/ProcessHandle.cs` per §4.3.
  `IProcessHandle` adds `Task WaitForExitAsync(CancellationToken)` (needed by
  `ShutdownAsync`; not in the §4.3 sketch, listed here for completeness).
- `ProcessManager` takes optional `IProcessLauncher` ctor param (defaults to
  `ProcessLauncher`); `GetProcessAsync` now returns `Task<IProcessHandle>`
  (`IProcessManager` + `MockProcessManager` updated; the integration
  `ProcessManagerTests` use `var` + `ShouldBeSameAs`, so they pass unchanged).
- DI: `AddSingleton<IProcessLauncher, ProcessLauncher>()`; the
  `ProcessManager` factory passes `launcher:` from DI.
- `BuildIgnoreSet`/`IsIgnoredFile` now `internal static`; `InternalsVisibleTo`
  added to `LuaHelperMcpServer.csproj`.
- Verified: `dotnet build` 0 warnings; 35 unit + 5 integration (real
  `cmd.exe`) green. The 5 `LspClientIntegrationTests` still skip via the
  pre-existing `Assert.Ignore` (converted in Step 6).
- **Recorded finding (integration project):** the two `ConditionalAccessQualifier...`
  warnings in `LspClientIntegrationTests.TearDown` were initially fixed by
  dropping `?.`, which caused NREs because NUnit runs `TearDown` after a failed
  `SetUp` (fields never assigned). Reverted to `?.` — intentionally retained
  until Step 6 restructures the file with `IntegrationTestFixture`.

### 4.3 `ProcessManager` testability seam

`ProcessManager` is `sealed` and spawns `System.Diagnostics.Process` directly,
so its backoff/restart/shutdown logic cannot be unit-tested without a real OS
process. Introduce a minimal process-creation seam so the logic is unit-testable
while keeping the production behaviour identical.

**New interface** `Services/IProcessLauncher.cs`:

```csharp
public interface IProcessLauncher
{
    IProcessHandle Start(ProcessStartInfo startInfo);
}
```

**New interface** `Services/IProcessHandle.cs` (wraps the parts of `Process`
that `ProcessManager` uses):

```csharp
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
    void WaitForExit(int millisecondsTimeout);
    void Kill(bool entireProcessTree);
    void Dispose();
}
```

**Production** `Services/ProcessLauncher.cs` + `ProcessHandle.cs` wrap
`System.Diagnostics.Process` (default, registered in DI). `ProcessManager`
takes `IProcessLauncher` via constructor (additional optional parameter
defaulting to a `ProcessLauncher` instance to keep `Program.cs`/DI simple).
`OnProcessExited`, restart counter, backoff index, max-restart throw, shutdown
timeout→force-kill, stderr drain all operate on `IProcessHandle`.

**Unit tests** then inject a `FakeProcessLauncher` (in `Tests.Unit/Helpers/`)
that returns a `FakeProcessHandle` (in-memory streams, manual exit control).

Update `.github/ARCHITECTURE.md` §3.3 (Process Crash Recovery) and
`process-lifecycle` skill to mention the seam. Update `ServiceCollectionExtensions`
to register `IProcessLauncher` → `ProcessLauncher`.

### 4.4 Integration-test seam for the MCP server binary

New helper `Tests.Integration/Infrastructure/McpStdioClient.cs` (see §7.1)
spawns the **real** server and speaks newline-delimited JSON-RPC. No
production-code change required.

## 5. Unit test plan (`LuaHelperMcpServer.Tests.Unit`)

All assertions use Shouldly (`Case.Sensitive`/`Case.Insensitive` explicit).
No filesystem, no real processes. Add `NSubstitute` + `AutoFixture` to the
integration project is **not** needed; the unit project already has them.

### 5.1 New / expanded test classes

#### `Services/ProcessManagerTests.cs` (NEW — moves logic tests into unit; keep the real-`cmd.exe` ones in integration)

Using `FakeProcessLauncher` + `FakeProcessHandle`:

| Test | Covers |
|---|---|
| `EnsureRunningAsync_NotRunning_SpawnsViaLauncher` | spawn path, `IsRunning` true |
| `EnsureRunningAsync_AlreadyRunning_DoesNotSpawnAgain` | early return |
| `EnsureRunningAsync_ExeMissing_ThrowsFileNotFoundException` | `VerifyExecutableExists` |
| `EnsureRunningAsync_AfterMaxRestarts_ThrowsInvalidOperation` | restart counter reset + throw (use `maxRestarts:0` + backoff `[]`) |
| `EnsureRunningAsync_OnRestart_AppliesBackoff` | backoff schedule index selection (`backoffSchedule` with tiny delays) |
| `OnProcessExited_IncrementsRestartAttempts_AndRaisesEvent` | `ProcessExited` event |
| `ShutdownAsync_GracefulExit_WaitsForExit` | `WaitForExit` success |
| `ShutdownAsync_Timeout_ForceKills` | timeout → `Kill(true)` |
| `ShutdownAsync_AlreadyExited_Noop` | early return |
| `ForceKill_AlreadyExited_Noop` | early return |
| `ForceKill_Running_KillsAndWaits` | `Kill` + `WaitForExit` |
| `Dispose_RunningProcess_KillsAndDisposes` | dispose path |
| `Dispose_Idempotent` | second dispose noop |
| `GetStreams_NotRunning_Throws` | guard |
| `GetProcessAsync_NotRunning_Throws` | guard |
| `ReadStderrAsync_EndOfStream_StopsCleanly` | stderr drain termination |

#### `Tools/LuaDiagnosticToolsTests.cs` (EXPAND — currently only 2 tests)

With `ILspClient`, `IDiagnosticCache`, `IConfigService` mocked via NSubstitute:

| Test | Covers |
|---|---|
| `CheckLuaFile_FileNotFound_ReturnsErrorString` (use a path that does not exist) | error path |
| `CheckLuaFile_ValidFile_ReturnsFormattedDiagnostics` | success path, `FormatDiagnostics` |
| `CheckLuaFile_NoDiagnostics_ReturnsNoWarningsString` | empty-diagnostics branch |
| `CheckLuaFile_WhenLspCrashed_ShutsDownAndReinitializes` | `EnsureLspReadyAsync` crash-recovery branch (set `State==Crashed`, first `EnsureInitializedAsync` throws `InvalidOperationException`) |
| `CheckLuaProject_DirNotFound_ReturnsErrorString` | error path |
| `CheckLuaProject_NoLuaFiles_ReturnsNoWarnings` | empty project |
| `CheckLuaProject_FiltersIgnoredDirs` | `BuildIgnoreSet` + `IsIgnoredFile` (create temp `.lua` files under an ignored dir name — use a temp dir under `AppContext.BaseDirectory`? **No FS in unit.** Instead refactor `BuildIgnoreSet`/`IsIgnoredFile` to be testable directly — they are `private static`; either make them `internal static` + `InternalsVisibleTo`, or test via `CheckLuaProject` with a mocked `Directory.EnumerateFiles`. Since `Directory.EnumerateFiles` is static and not mockable, **extract an `IFileEnumerator`** or make the helpers `internal static` and unit-test them directly. **Decision:** make `BuildIgnoreSet`/`IsIgnoredFile` `internal static` and add `InternalsVisibleTo("LuaHelperMcpServer.Tests.Unit")` to the server project; test them directly. Record this in ARCHITECTURE.md.) |
| `CheckLuaProject_DiagnosticsTimeout_ReturnsEmptyForThatFile` | `GetDiagnosticsAsync` throws `TimeoutException` → empty list |
| `GetSupportedChecks_ReturnsAll21Checks` (exists) | keep |
| `GetLuahelperVersion_ReturnsConfiguredVersion` (exists) | keep |

#### `Resources/DiagnosticResourcesTests.cs` (NEW)

With mocked `ILspClient`, `IDiagnosticCache`, `IConfigService`:

| Test | Covers |
|---|---|
| `GetDiagnostics_FileNotFound_ThrowsMcpException` | resource error path |
| `GetDiagnostics_Cached_ReturnsCachedJson` | cache-hit branch |
| `GetDiagnostics_Uncached_InitializesAndReturnsJson` | cache-miss + LSP init |
| `GetDiagnostics_Timeout_ReturnsEmptyArray` | `TimeoutException` branch |
| `GetDiagnostics_CrashedState_Reinitializes` | crash recovery |
| `GetConfig_NoProjectPath_ReturnsDefaultConfig` | empty `LuaHelperConfig` path |
| `GetConfig_WithProjectPath_ReturnsServiceConfig` | delegated path |

#### `Models/DiagnosticCollectionTests.cs` (NEW)

| Test | Covers |
|---|---|
| `ToFormattedString_NoDiagnostics_ReturnsNoWarnings` | `TotalCount==0` branch |
| `ToFormattedString_WithDiagnostics_ListsFilesAndCounts` | summary + per-file formatting, `file:///` → `\` rewrite |
| `ToFormattedString_MixesFilesWithAndWithoutDiagnostics` | `Where(k => k.Value.Count > 0)` filter, file-count calc |
| `TotalCount_SumsAcrossFiles` | `ByFile.Values.Sum` |

#### `Models/LuaDiagnosticTests.cs` (NEW)

| Test | Covers |
|---|---|
| `ToFormattedString_ReturnsExpectedFormat` | `L{Line}:{Char} [{Severity}] {Message}` |
| `Defaults_UriAndMessageEmpty_SourceNull` | init defaults |

#### `Models/LuahelperJsonTemplateTests.cs` (NEW)

| Test | Covers |
|---|---|
| `Serialize_ProducesExpectedDefaults` | serialize `new LuahelperJsonTemplate()` and assert exact JSON fields (covers all auto-property initializers — the 65 instrumentable lines) |
| `Defaults_ContainWoWGlobalsAndIgnoreDirs` | assert `IgnoreModules` contains `C_Container` etc., `IgnoreFileOrFloder` contains `.vscode/`, `Tests/`, etc. |

#### `Prompts/LuaHelperPromptsTests.cs` (NEW)

| Test | Covers |
|---|---|
| `FixLuaWarnings_ReturnsUserChatMessageWithPath` | exact message text |
| `ConfigureLuahelper_ReturnsUserChatMessageWithPath` | exact message text |

#### `Extensions/LualspPathResolverTests.cs` (NEW)

| Test | Covers |
|---|---|
| `Resolve_NullOrWhitespace_ReturnsDefaultCombinedWithBaseDir` | null/empty/whitespace |
| `Resolve_RelativePath_CombinesWithBaseDir` | relative |
| `Resolve_RootedPath_ReturnedAsIs` | rooted |
| `DefaultLualspPath_ReturnsPlatformPath` | (guard with `OperatingSystem` helpers; assert the Windows branch on Windows CI) |

#### `Extensions/ServiceCollectionExtensionsTests.cs` (NEW)

| Test | Covers |
|---|---|
| `AddLuaHelperServices_RegistersAllServicesAsSingletons` | build `ServiceCollection`, call `AddLuaHelperServices`, assert `GetService<IFileReader>() is FileReader`, `IDiagnosticCache is DiagnosticCache`, `IConfigService is ConfigService`, `IProcessManager is ProcessManager`, `ILspClient is LspClient`, `IProcessLauncher is ProcessLauncher` |
| `AddLuaHelperServices_ProcessManagerUsesOptionsBackoff` | resolve `IProcessManager` with configured `MaxRestarts`/`BackoffScheduleSeconds` (via `Options.Configure`) and assert via a probe — or assert the factory does not throw |

#### `Services/LspMessageReaderTests.cs` (EXPAND)

| Test | Covers |
|---|---|
| `ReadMessageAsync_MalformedHeaderNoContentLength_SkipsToNextMessage` | the recursive `TryParseMessage` skip branch (line ~67–69) |
| `ReadMessageAsync_TwoMessagesInOneBuffer_ReturnsBothSequentially` | buffer reuse after `RemoveRange` |
| `ReadMessageAsync_HeaderSplitAcrossReads_Parses` | partial header + partial body across multiple reads (use `PartialWriteStream`) |

#### `Services/LspClientTests.cs` (EXPAND)

| Test | Covers |
|---|---|
| `OpenFileAsync_NotReady_ThrowsInvalidOperationException` | state guard |
| `OpenFileAsync_FileNotFound_ThrowsFileNotFoundException` | `IFileReader.FileExists` false |
| `GetDiagnosticsAsync_AlreadyCached_ReturnsCached` | cache-hit early return |
| `GetDiagnosticsAsync_Timeout_ReturnsCacheOrEmpty` | 10 s timeout path (use a `FakeLspServer` variant that never publishes) |
| `ReadLoop_UnexpectedExit_SetsStateToCrashed` | stream ends → `Crashed` |
| `DispatchMessage_WindowLogMessage_Noop` | `window/logMessage` case |
| `DispatchMessage_UnknownNotification_LogsDebug` | default case |
| `EnsureInitializedAsync_SameProjectTwice_IsNoop` | `Ready && projectPath==` early return |
| `ShutdownAsync_WhenNotStarted_IsNoop` | `NotStarted` early return |

#### `Services/ConfigServiceTests.cs` (EXPAND)

| Test | Covers |
|---|---|
| `GetConfig_MalformedJson_LogsAndReturnsDefaults` | `JsonException` catch (line ~45) |
| `GetConfig_IndividualCheckFlagOverrides` | each `TryMergeCheckFlags` branch — parametrized `[TestCase]` per flag (21 cases) |
| `GetConfig_EnableReport_Merged` | `EnableReport` branch |
| `GetConfig_AllEnableBool_WinsOverShowWarnFlag` | `AllEnable` bool present + `ShowWarnFlag` present → bool wins (branch order) |
| `Merge_FallsBackToCaseInsensitivePropertyName` | `TryGetProperty` ordinal-ignorecase fallback (use a JSON with differing case) |

#### `Tools/ConfigToolsTests.cs` (EXPAND)

| Test | Covers |
|---|---|
| `CreateLuahelperJson_ValidDir_CallsCreateDefaultConfig` | success path (mock `IConfigService.CreateDefaultConfig` returning a string) |

### 5.2 Helper additions (`Tests.Unit/Helpers/`)

- `FakeProcessLauncher.cs` + `FakeProcessHandle.cs` — in-memory `IProcessLauncher`
  with controllable `StandardInput`/`StandardOutput` (anonymous pipes or
  `MemoryStream`), `IsRunning`, `HasExited`, manual `RaiseExited()`.
- Extend `FakeLspServer` with a mode that never sends diagnostics (for the
  `GetDiagnosticsAsync_Timeout` test) — add a constructor flag
  `publishDiagnostics: bool`.

### 5.3 Project changes

- Add `<InternalsVisibleTo Include="LuaHelperMcpServer.Tests.Unit" />` to
  `LuaHelperMcpServer.csproj` (so `BuildIgnoreSet`/`IsIgnoredFile` can be
  `internal static` and tested directly).

#### Step 4 outcome (2026-08-14) — unit tests >80 %

- Implemented the full §5.1 matrix (16 new ProcessManager tests, 13 new
  LuaDiagnosticTools tests, 7 DiagnosticResources, 3 DiagnosticCollection,
  2 LuaDiagnostic, 1 LuahelperJsonTemplate, 2 LuaHelperPrompts, 4
  LualspPathResolver, 3 ServiceCollectionExtensions, 4 new
  LspMessageReader tests, 8 new LspClient tests, 23 new ConfigService
  cases, 1 new ConfigTools case) + `Helpers/FakeProcessLauncher.cs`,
  `Helpers/FakeProcessHandle.cs` (§5.2).
- **Total: 127 unit tests green** (was 35). Hand-written line coverage
  **94.2 %** (1264/1342 lines, excluding `obj\`-generated source and
  `Program.cs` per the §8.3 filter) — comfortably above the >80 % gate.
- **Deviations from the §5.1 sketch (recorded contract-first):**
  1. `EnsureRunningAsync_AfterMaxRestarts_ThrowsInvalidOperation` uses
     `maxRestarts: 2` + two `RaiseExited()` calls instead of
     `maxRestarts: 0` (the 0-case is just the guard; the realistic path
     exercises the counter too). `EnsureRunningAsync_AfterThrow_ResetsRestartCounter`
     covers the reset.
  2. Backoff verification (`EnsureRunningAsync_OnRestart_AppliesBackoff`)
     uses a 30 s backoff + a 100 ms-cancelled token → `OperationCanceledException`,
     proving the delay was awaited (deterministic, no timing flakiness).
  3. The plan's `FakeLspServer` "never publishes" mode (§5.2) was **not**
     added; the timeout tests instead pre-store file content in the
     `DiagnosticCache` (skips `OpenFileAsync`, which is the only path that
     needs a ready state) and let the hardcoded 10 s timeout fire. Two
     intentional slow tests: `GetDiagnosticsAsync_Timeout_ReturnsEmptyList`
     and `CheckLuaProject_DiagnosticsTimeout_ReturnsEmptyForThatFile` (~10 s
     each).
  4. `CheckLuaProject_WithDiagnostics_ReturnsSummary` and
     `CheckLuaProject_DiagnosticsTimeout_ReturnsEmptyForThatFile` create a
     self-cleaning temp dir (plan §5.1 banned FS for the *ignore-filter*
     test only; the two project tests need real `.lua` files because
     `Directory.EnumerateFiles` is static and unmockable).
  5. `GetDiagnosticsAsync_Timeout_ReturnsEmptyList` (LspClient) is named
     `..._ReturnsEmptyList` rather than `..._ReturnsCacheOrEmpty`: with
     diagnostics pre-cached the method returns at the top, so the
     cache-return branch of the OCE path is unreachable through the public
     API in a deterministic test.
- `FakeLspServer` gained `CloseOutput()` (stream EOF → read loop exits →
  `Crashed`), `SendWindowLogMessage()`, `SendUnknownNotification()`;
  `MockProcessManager` gained `EnsureRunningAsyncCalls` counter.
- Unit project is analyzer-clean after Step 4 code (ReSharper
  `severity=warning`, `noBuild=true`: 10 findings in new tests fixed, re-run
  clean); `dotnet build` 0 warnings; `csharpier format src` applied.
- **Coverage report (Debug, this machine):** line-rate 0.7611 overall
  (2648/3479) including generated source; hand-written 94.2 %. The §8.3 CI
  filter (`obj\`-excluded + `Program.cs`) is what the gate measures.

## 6. Integration test plan (`LuaHelperMcpServer.Tests.Integration`)

### 6.1 Principles (per the user's explicit requirements)

- Drive the **real** `lualsp.exe` and the **real** `LuaHelperMcpServer` binary.
- Send real JSON-RPC over **stdin**, read **stdout**, compare against
  **exact expected** values (golden). On `lualsp.exe` upgrade, update fixtures +
  expected values together.
- **No `Assert.Ignore`.** If a required binary is missing, the test **fails**
  with a clear message.
- **MCP stdio framing is newline-delimited JSON-RPC** (one message per line) —
  NOT `Content-Length`. (See `mcp-sdk-csharp/SKILL.md`.) The LSP layer between
  server and `lualsp.exe` still uses `Content-Length`, but that is internal.

### 6.2 Binary resolution (no skip — fail clearly if missing)

`IntegrationTestFixture` (one per test run) resolves, in order:

1. `lualsp.exe`:
   - `LUAHELPER_LUALSP_PATH` env → use directly.
   - else `LUAHELPER_EXTENSION_PATH\server\lualsp.exe`.
   - else `<repoRoot>\lualsp\win-x64\lualsp.exe`.
   - If none exists → `Assert.Fail("lualsp.exe not found. Run .github/tools/fetch-lualsp.ps1 first.")`.
2. `LuaHelperMcpServer` binary:
   - `LUAHELPER_MCP_SERVER_PATH` env (exe or dll) → use as-is (exe) or `dotnet <dll>`.
   - else default: `dotnet <repoRoot>\src\LuaHelperMcpServer\bin\Release\net10.0\LuaHelperMcpServer.dll` (or `Debug` if Release absent).
   - If absent → `Assert.Fail(...)`.

The fixture sets `LUAHELPER_LUALSP_PATH` (absolute) on every spawned server
process so the server's `LualspPathResolver` finds `lualsp.exe` regardless of
working directory.

### 6.3 Test infrastructure (new)

#### `Infrastructure/McpStdioClient.cs` (NEW)

Spawns the real server process (`ProcessStartInfo` with redirected stdin/stdout/
stderr), reads stderr to a log (deadlock prevention), and provides:

```csharp
public sealed class McpStdioClient : IAsyncDisposable
{
    public McpStdioClient(string serverCommand, string lualspPath, string workingDir);
    public Task InitializeAsync(CancellationToken ct);   // id=1 initialize + notifications/initialized
    public Task<JsonNode> CallAsync(string method, JsonNode? @params, CancellationToken ct); // sends id, awaits matching response
    public Task<JsonNode> CallToolAsync(string toolName, JsonNode arguments, CancellationToken ct);
    public Task<JsonNode> ReadResourceAsync(string uri, CancellationToken ct);
    public Task<JsonNode> GetPromptAsync(string name, JsonNode arguments, CancellationToken ct);
    public IReadOnlyList<string> StderrLines { get; }
}
```

Wire format: each request is a single JSON object written with
`StreamWriter.WriteLineAsync` (newline-delimited). Responses are read with
`StreamReader.ReadLineAsync` and matched by `id`. A background task drains
stderr into a list (fail the test on deadlock if stderr fills). Timeout per
response: 30 s (configurable).

#### `Infrastructure/GoldenAssert.cs` (NEW)

`AssertJsonEquals(expectedJson, actualJson)` — parses both, compares with
`JsonNode.DeepEquals` after normalizing key order, and on mismatch prints a
unified diff of the serialized (indented) JSON. Used by all golden comparisons.

#### `Fixtures/` (expand)

Existing: `test_with_warning.lua`, `test_clean.lua`.
Add (one fixture per diagnostic scenario we assert exactly):

| Fixture | Produces | Asserted by |
|---|---|---|
| `test_syntax_error.lua` | `local x =` (incomplete) → Error severity | LSP + MCP `check_lua_file` |
| `test_undefined_global.lua` | uses undefined `C_Container` (no ignore) → Warning | LSP + MCP |
| `test_unused_local.lua` | `local x = 1` not used (CheckLocalNoUse) | LSP |
| `test_duplicate_table_key.lua` | `{ a=1, a=2 }` | LSP |
| `test_float_eq.lua` | `if 0.1 == 0.2 then` | LSP |
| `test_self_assign.lua` | `x = x` | LSP |
| `project_with_luahelper_json/` | `luahelper.json` + `Main.lua` | MCP `get_luahelper_config`, `check_lua_project` |

Each fixture's **expected diagnostics are recorded as a golden JSON file**
alongside (e.g. `test_with_warning.lua.expected.json`) containing the exact
`range`/`severity`/`warningType`/`message`. When `lualsp.exe` is upgraded and
output changes, the developer re-runs, updates the `.expected.json`, and commits
both.

### 6.4 LSP-layer integration tests (expand `LspClientIntegrationTests`)

Real `lualsp.exe`, via `LspClient` + `ProcessManager` (existing pattern).

| Test | Asserts (golden) |
|---|---|
| `CheckFile_WithWarning_MatchesGolden` (exists, tighten to golden) | exact diagnostics == `test_with_warning.lua.expected.json` |
| `CheckFile_Clean_ReturnsNoDiagnostics` (exists) | empty |
| `CheckMultipleFiles_AllDiagnosticsReturned` (exists) | exact per-file |
| `CheckFile_SyntaxError_SeverityIsError` (NEW) | severity == Error, exact message |
| `CheckFile_UndefinedGlobal_MatchesGolden` (NEW) | golden |
| `CheckFile_CheckLocalNoUse_MatchesGolden` (NEW) | golden (enable flag) |
| `CheckFile_DuplicateTableKey_MatchesGolden` (NEW) | golden |
| `CheckFile_FloatEq_MatchesGolden` (NEW) | golden (enable CheckFloatEq) |
| `LuahelperJson_IgnoredModules_ProduceNoDiagnostics` (exists) | empty |
| `LuahelperJson_MissingIgnoreModules_FlagsUndefinedGlobal` (exists) | exact |
| `Reinitialize_SameProject_IsNoop` (NEW) | state stays Ready, no re-init |
| `Reinitialize_DifferentProject_Reinitializes` (NEW) | new projectPath |
| `Shutdown_ThenReopen_Works` (NEW) | state transitions |
| `CrashRecovery_AfterProcessExit_RestartsAndRechecks` (NEW) | kill lualsp mid-session (via `ProcessManager.ForceKill`), re-call `CheckLuaFile`, asserts diagnostics still returned |

### 6.5 MCP-layer end-to-end tests (NEW — `McpServerIntegrationTests.cs`)

Real `LuaHelperMcpServer` binary via `McpStdioClient`. Each test does the full
handshake (`initialize` → `notifications/initialized`) in `[SetUp]`; `[TearDown]`
disposes the client (kills the server).

#### 6.5.1 Capability discovery

| Test | Asserts |
|---|---|
| `Initialize_ReturnsServerInfoAndCapabilities` | `result.serverInfo.name`, `result.capabilities.tools` true |
| `ToolsList_ExposesAllSevenTools` | exact set: `get_server_version`, `get_luahelper_version`, `get_supported_checks`, `get_luahelper_config`, `create_luahelper_json`, `check_lua_project` |
| `ResourcesList_ExposesDiagnosticsAndConfig` | exact URIs: `luahelper://diagnostics/{+filePath}`, `luahelper://config` |
| `PromptsList_ExposesBothPrompts` | exact names: `fix_lua_warnings`, `configure_luahelper` |

#### 6.5.2 Tools (all 7)

| Test | Tool | Asserts (golden/exact) |
|---|---|---|
| `GetServerVersion_MatchesAssemblyVersion` | `get_server_version` | result text == `ExpectedVersion` (read from `version.json`/csproj, e.g. `0.1.0`) |
| `GetLuahelperVersion_MatchesBundledLualsp` | `get_luahelper_version` | `LuaHelper lualsp.exe v<version.json.version>` |
| `GetSupportedChecks_ReturnsExact21Checks` | `get_supported_checks` | JSON == `Fixtures/supported_checks.expected.json` |
| `GetLuahelperConfig_ProjectWithLuahelperJson_MatchesGolden` | `get_luahelper_config` (project_with_luahelper_json) | JSON == `Fixtures/project_with_luahelper_json/config.expected.json` |
| `GetLuahelperConfig_ProjectWithoutLuahelperJson_ReturnsDefaults` | `get_luahelper_config` (clean project) | JSON == `Fixtures/default_config.expected.json` |
| `CreateLuahelperJson_CreatesFileWithExactContent` | `create_luahelper_json` (temp dir) | created file == `Fixtures/luahelper_json_template.expected.json`; cleanup dir |
| `CreateLuahelperJson_InvalidDir_ReturnsError` | `create_luahelper_json` (nonexistent) | `isError:true` or text contains `Error: Directory not found` |
| `CheckLuaFile_WithWarning_MatchesGolden` | `check_lua_file` (`test_with_warning.lua`) | result text == golden (diagnostic count + exact line/severity/message) |
| `CheckLuaFile_Clean_ReturnsNoWarnings` | `check_lua_file` (`test_clean.lua`) | text == `No warnings found in ...` |
| `CheckLuaFile_FileNotFound_ReturnsError` | `check_lua_file` (nonexistent) | text == `Error: File not found: ...` |
| `CheckLuaFile_SyntaxError_MatchesGolden` | `check_lua_file` (`test_syntax_error.lua`) | golden |
| `CheckLuaProject_CleanProject_ReturnsNoWarnings` | `check_lua_project` (clean dir) | text == `No warnings found in project ...` |
| `CheckLuaProject_WithWarnings_MatchesGolden` | `check_lua_project` (fixtures dir) | summary + per-file == golden |
| `CheckLuaProject_DirNotFound_ReturnsError` | `check_lua_project` (nonexistent) | text == `Error: Directory not found: ...` |

#### 6.5.3 Resources (2)

| Test | Asserts |
|---|---|
| `ReadDiagnosticsResource_WithWarning_MatchesGolden` | `resources/read` `luahelper://diagnostics/<file>` → JSON == `test_with_warning.lua.expected.json` |
| `ReadDiagnosticsResource_FileNotFound_ReturnsError` | `result.isError:true`, message contains `File not found` |
| `ReadConfigResource_NoProject_ReturnsDefaultConfig` | JSON == `default_config.expected.json` |
| `ReadConfigResource_WithProject_MatchesGolden` | JSON == `project_with_luahelper_json/config.expected.json` |

#### 6.5.4 Prompts (2)

| Test | Asserts |
|---|---|
| `GetFixLuaWarningsPrompt_ReturnsExactMessage` | messages[0].content == `Analyze the Lua file at <path> and suggest fixes for all warnings reported by LuaHelper.` |
| `GetConfigureLuahelperPrompt_ReturnsExactMessage` | messages[0].content == `Help me configure luahelper.json for my Lua project at <path>. Consider the WoW API globals that should be ignored.` |

### 6.6 Project changes (integration)

- Add package references: `System.Text.Json` (for `JsonNode`), `Microsoft.Extensions.Logging`
  (already present). No mocking libs (integration uses real binaries).
- Add `<Content Include="Fixtures\**\*.expected.json" CopyToOutputDirectory="PreserveNewest" />`
  (already covers `Fixtures\**`).
- Remove all `Assert.Ignore` usages; replace with `Assert.Fail` in
  `IntegrationTestFixture` binary resolution.

## 7. Test infrastructure summary

### 7.1 New files

| Path | Purpose |
|---|---|
| `src/LuaHelperMcpServer/Services/IProcessLauncher.cs` | Process spawn seam (§4.3) |
| `src/LuaHelperMcpServer/Services/IProcessHandle.cs` | Process abstraction (§4.3) |
| `src/LuaHelperMcpServer/Services/ProcessLauncher.cs` | Production wrapper (§4.3) |
| `src/LuaHelperMcpServer/Services/ProcessHandle.cs` | Production wrapper (§4.3) |
| `src/LuaHelperMcpServer.Tests.Unit/Helpers/FakeProcessLauncher.cs` | Unit fake (§5.2) |
| `src/LuaHelperMcpServer.Tests.Unit/Helpers/FakeProcessHandle.cs` | Unit fake (§5.2) |
| `src/LuaHelperMcpServer.Tests.Unit/Resources/DiagnosticResourcesTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Unit/Models/DiagnosticCollectionTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Unit/Models/LuaDiagnosticTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Unit/Models/LuahelperJsonTemplateTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Unit/Prompts/LuaHelperPromptsTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Unit/Extensions/LualspPathResolverTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Unit/Extensions/ServiceCollectionExtensionsTests.cs` | §5.1 |
| `src/LuaHelperMcpServer.Tests.Integration/Infrastructure/McpStdioClient.cs` | §6.3 |
| `src/LuaHelperMcpServer.Tests.Integration/Infrastructure/IntegrationTestFixture.cs` | §6.2 |
| `src/LuaHelperMcpServer.Tests.Integration/Infrastructure/GoldenAssert.cs` | §6.3 |
| `src/LuaHelperMcpServer.Tests.Integration/McpServerIntegrationTests.cs` | §6.5 |
| `Fixtures/*.expected.json`, `Fixtures/project_with_luahelper_json/**` | golden data |

### 7.2 Modified files

| Path | Change |
|---|---|
| `Models/InitializeOptions.cs` | **delete** (dead code, analyzer-confirmed per §4.2) |
| `Services/ProcessManager.cs` | use `IProcessLauncher`; unseal-or-keep-sealed (keep sealed, inject launcher) |
| `Extensions/ServiceCollectionExtensions.cs` | register `IProcessLauncher` → `ProcessLauncher` |
| `Tools/LuaDiagnosticTools.cs` | `BuildIgnoreSet`/`IsIgnoredFile` → `internal static` |
| `LuaHelperMcpServer.csproj` | add `InternalsVisibleTo` |
| `Tests.Integration/Services/LspClientIntegrationTests.cs` | remove `Assert.Ignore`, tighten to golden, add new scenarios |
| `Tests.Integration/Services/ProcessManagerTests.cs` | (keep real-cmd tests; remove any `Assert.Ignore`) |

## 8. CI changes (`.github/workflows/ci.yml`)

The `build-test` job already provisions `lualsp.exe` via `fetch-lualsp.ps1`
and runs both test projects. Required adjustments:

1. **Build the server in Release** before integration tests (so the MCP-layer
   harness can resolve `LuaHelperMcpServer.dll`):
   ```yaml
   - name: Build solution (Release)
     shell: pwsh
     run: .\.github\tools\build.ps1 -Configuration Release
   ```
2. **Set `LUAHELPER_LUALSP_PATH`** (absolute) for the integration run so the
   spawned MCP server finds `lualsp.exe`:
   ```yaml
   - name: Integration tests
     shell: pwsh
     run: |
       $exe = (Resolve-Path lualsp\win-x64\lualsp.exe).Path
       $env:LUAHELPER_LUALSP_PATH = $exe
       $env:LUAHELPER_MCP_SERVER_PATH = "dotnet;$((Resolve-Path src\LuaHelperMcpServer\bin\Release\net10.0\LuaHelperMcpServer.dll).Path)"
       dotnet test src\LuaHelperMcpServer.Tests.Integration -c Release
   ```
   (`McpStdioClient` splits `LUAHELPER_MCP_SERVER_PATH` on `;` into command + arg.)
3. **Add a coverage gate** to the unit-test step:
   ```yaml
   - name: Unit tests + coverage
     shell: pwsh
     run: |
       dotnet test src\LuaHelperMcpServer.Tests.Unit -c Release `
         --collect:"XPlat Code Coverage" `
         --results-directory ./coverage
       $xml = Get-ChildItem -Recurse ./coverage -Filter coverage.cobertura.xml | Select-Object -First 1
       [xml]$c = Get-Content $xml.FullName
       # Exclude generated source by filtering <class> with filename not containing '\obj\'
       ...compute hand-written line-rate...
       if ($rate -lt 0.80) { throw "Unit coverage $rate < 0.80" }
   ```
   (Implement the hand-written-only filter as in §2; or configure coverlet
   filtering via `<Exclude>`/`<ExcludeByAttribute>` in the csproj to drop
   generated source and `Program.cs` from the denominator.)
4. The `aot-win-x64` job's `smoke-test-mcp.ps1` stays as a separate AOT smoke
   test (it already exercises `initialize`/`tools/list`/`tools/call`); the new
   `McpServerIntegrationTests` cover the same surface more thoroughly via the
   managed dll in the `build-test` job.

## 9. Documentation updates (do alongside code, contract-first)

Per AGENTS.md "update `.github/` first":

- `nunit-testing/SKILL.md` — replace "integration tests skip gracefully with
  `Assert.Ignore`" with "integration tests must **fail** if a required binary
  is missing; CI provisions binaries via `fetch-lualsp.ps1`". Add the
  `McpStdioClient` + golden-assertion pattern.
- `CONTEXT.md` "Testing" section — same change; add the `>80 %` coverage gate
  and the `IProcessLauncher` seam note.
- `ARCHITECTURE.md` §3.3 (Process Crash Recovery) and §10 (Testing Strategy) —
  document the `IProcessLauncher` seam and the new MCP-layer integration suite;
  update the test pyramid to show the MCP end-to-end layer.
- `dev-plan-luahelper-mcp-server.md` — add a "Test hardening" phase referencing
  this plan; mark the original "graceful skip" decision as superseded; record
  the analyzer-driven refactoring gate and its findings/outcomes.
- `README.md` — no change (human-facing only).

## 10. Definition of Done

- [ ] ReSharper inspections run on all 3 projects (`severity=warning`, `noBuild=true`, `timeoutSeconds=1500`); all reported warnings fixed; per-project re-run clean (unfixable ones documented in this plan)
- [ ] All analyzer-reported dead code removed (incl. `Models/InitializeOptions.cs`), each deletion verified via `dotnet-debugger_code_find_usages` == 0; no self-directed refactoring performed
- [ ] `IProcessLauncher`/`IProcessHandle` seam introduced; `ProcessManager`
      uses it; DI registers `ProcessLauncher`; production behaviour unchanged
      (existing integration `ProcessManagerTests` still pass).
- [ ] `InternalsVisibleTo` added; `BuildIgnoreSet`/`IsIgnoredFile` `internal static`.
- [ ] All unit tests in §5.1 implemented and green.
- [ ] `McpStdioClient` + `IntegrationTestFixture` + `GoldenAssert` implemented.
- [ ] All LSP-layer integration tests in §6.4 green against real `lualsp.exe`.
- [ ] All MCP-layer integration tests in §6.5 green against the real server
      binary; every tool/resource/prompt asserted with golden/exact values.
- [ ] No `Assert.Ignore` in the integration project.
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Unit --collect:"XPlat Code Coverage"`
      reports hand-written line coverage **> 80 %**.
- [ ] `ci.yml` updated: Release build, `LUAHELPER_LUALSP_PATH`/
      `LUAHELPER_MCP_SERVER_PATH` set, coverage gate enforced.
- [ ] `.github/` docs in §9 updated.
- [ ] `csharpier format src` clean; `dotnet build` clean.

## 11. Implementation order

1. **Contract-first docs:** apply §9 doc updates (mark `Assert.Ignore`
   guidance superseded, document the analyzer-driven gate and the seam).
2. **Analyzer-driven refactoring (§4.1–4.2):** run
   `dotnet-debugger_resharper_inspect_project` on the three projects
   (`severity=warning`, `noBuild=true`, `timeoutSeconds=1500`); fix **only**
   the findings — dead code (incl. `InitializeOptions.cs`) and warnings —
   verifying deletions with `dotnet-debugger_code_find_usages`; re-run
   inspections until each project is warning-free.
3. **Plan-mandated testability seam (§4.3–4.4):** add `IProcessLauncher` seam;
   wire DI; make helpers `internal static`; add `InternalsVisibleTo`. Verify
   existing tests still green.
4. **Unit tests (§5):** `ProcessManager` → `LuaDiagnosticTools` →
   `DiagnosticResources` → models/prompts/extensions → expand
   reader/client/config. Re-measure coverage; iterate to > 80 %.
5. **Integration infra (§6.3):** `McpStdioClient`, fixture, `GoldenAssert`,
   fixtures + golden JSON.
6. **LSP-layer integration (§6.4):** tighten existing to golden; add new
   scenarios.
7. **MCP-layer integration (§6.5):** discovery → tools → resources → prompts.
8. **CI (§8):** Release build, env vars, coverage gate.
9. **Verify DoD (§10);** run `csharpier format src`; final coverage check.
