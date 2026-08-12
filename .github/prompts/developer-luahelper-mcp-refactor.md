# Prompt: Developer — LuaHelper MCP Server Phase 0 Refactoring

> Use this agent: `dotnet-senior-developer` (or any coding agent)
> Context:
>   - `.github\docs\arch-luahelper-mcp-server.md` — architecture
>   - `.github\docs\dev-plan-luahelper-mcp-server.md` — development plan
>   - `.github\docs\audit-phase0.md` — code audit findings
>   - `.github\prompts\developer-luahelper-mcp.md` — original developer prompt

## Task

Refactor the Phase 0 code of the LuaHelper MCP Server to fix all issues identified in the code audit (`audit-phase0.md`). This is a **refactoring pass** — do not add new features, only fix, restructure, and improve existing code.

## Rules

1. **Read the audit first** — `audit-phase0.md` contains every issue with file, line, and severity.
2. **Fix Critical issues first**, then High, then Medium.
3. **Do not change observable behavior** — this is refactoring, not feature work.
4. **Commit after each logical group of changes** — small, atomic commits.
5. **Tests must pass after every commit**.
6. **Format with CSharpier** after all code changes are done.

---

## Critical Fixes

### 1. Empty Catch Block — `LspClient.ShutdownAsync`

**File:** `src/LuaHelperMcpServer/Services/LspClient.cs`

Replace the empty catch:
```csharp
try
{
    await _writer!.SendNotificationAsync("exit", null, CancellationToken.None);
}
catch
{
    // Ignore — process may already be gone
}
```

With:
```csharp
try
{
    await _writer!.SendNotificationAsync("exit", null, CancellationToken.None);
}
catch (Exception ex)
{
    _logger.LogDebug(ex, "Exit notification failed — process likely already exited");
}
```

### 2. Hardcoded Paths — `Program.cs`

**File:** `src/LuaHelperMcpServer/Program.cs`

Remove hardcoded `LualspPath` and `PluginPath` constants. Instead, read them from environment variables with fallback to a reasonable default, or accept them as command-line arguments. The integration tests must NOT reference `C:\Users\dnmno\...` paths.

### 3. State Bug — `ReadLoopAsync` Sets `Crashed` on Graceful Shutdown

**File:** `src/LuaHelperMcpServer/Services/LspClient.cs`

The `finally` block in `ReadLoopAsync` always sets `_state = LspState.Crashed`. This is wrong when the read loop ends due to cancellation (graceful shutdown).

Fix: Only set `Crashed` if the loop ended due to an unexpected error, not due to cancellation. Check `ct.IsCancellationRequested` in the finally block.

### 4. Missing Restart/Backoff Logic in `ProcessManager`

**File:** `src/LuaHelperMcpServer/Services/ProcessManager.cs`

The architecture doc specifies:
- Max 3 restart attempts per crash
- Exponential backoff: 2s → 4s → 8s
- After max retries: return error, reset on next manual request

Add the restart policy fields and logic. The `EnsureRunningAsync` method should track restart attempts and apply backoff before respawning.

---

## High Priority Fixes

### 5. Extract Long Methods (Target: 10-15 lines)

**File:** `src/LuaHelperMcpServer/Services/LspClient.cs`

| Method | Current Lines | Action |
|---|---|---|
| `EnsureInitializedAsync` | ~80 | Extract: `BuildInitializeParams()`, `SendInitializeRequest()`, `WaitForInitializeResponse()`, `SendInitializedNotification()` |
| `HandlePublishDiagnostics` | ~40 | Extract: `ParseDiagnostic()`, `ParseRange()` |
| `ReadLoopAsync` | ~35 | Extract: `DispatchMessage()`, `HandleResponse()`, `HandleNotification()` |
| `ShutdownAsync` | ~30 | Extract: `SendShutdownRequest()`, `SendExitNotification()` |

**File:** `src/LuaHelperMcpServer/Services/ProcessManager.cs`

| Method | Current Lines | Action |
|---|---|---|
| `EnsureRunningAsync` | ~35 | Extract: `CreateProcessStartInfo()`, `SpawnProcess()` |

**File:** `src/LuaHelperMcpServer/Services/LspMessageReader.cs`

| Method | Current Lines | Action |
|---|---|---|
| `TryParseMessage` | ~35 | Extract: `FindHeaderEnd()`, `ExtractBody()` |

### 6. Reduce Nesting Depth to ≤3

After extracting methods (fix #5), nesting should naturally reduce. Verify no method has more than 3 levels of `if`/`for`/`switch` nesting.

### 7. Replace "What" Comments with Well-Named Methods

Every comment that describes **what** the code does should be replaced by extracting that code into a method whose name describes the intent.

Examples:
- `// Send LSP initialize request` → `await SendInitializeRequestAsync(initParams, ct);`
- `// Wait for response with 30s timeout` → `var response = await WaitForInitializeResponseAsync(initTcs, ct);`
- `// Store file content for crash recovery` → `StoreFileForCrashRecovery(uri, content);`

Remove all "what" comments. Keep "why" comments (explaining rationale).

### 8. Migrate Tests to NUnit

**All test files:** Replace xUnit with NUnit.

Mapping:
| xUnit | NUnit |
|---|---|
| `[Fact]` | `[Test]` |
| `[Theory]` + `[InlineData]` | `[TestCase]` |
| `Assert.Equal(a, b)` | `Assert.That(b, Is.EqualTo(a))` |
| `Assert.NotNull(x)` | `Assert.That(x, Is.Not.Null)` |
| `Assert.Null(x)` | `Assert.That(x, Is.Null)` |
| `Assert.True(x)` | `Assert.That(x, Is.True)` |
| `Assert.False(x)` | `Assert.That(x, Is.False)` |
| `Assert.Empty(x)` | `Assert.That(x, Is.Empty)` |
| `Assert.NotEmpty(x)` | `Assert.That(x, Is.Not.Empty)` |
| `Assert.Contains(x, pred)` | `Assert.That(x, Has.Some.Matches(pred))` |
| `Assert.ThrowsAsync<T>(...)` | `Assert.ThrowsAsync<T>(...)` |
| `Assert.ThrowsAnyAsync<T>(...)` | `Assert.CatchAsync<T>(...)` |
| `Assert.Same(a, b)` | `Assert.That(b, Is.SameAs(a))` |
| `Assert.DoesNotContain(x, text)` | `Assert.That(text, Does.Not.Contain(x))` |
| `IDisposable` + `Dispose()` | `[TearDown]` method |
| Constructor setup | `[SetUp]` method |

Update `.csproj`:
- Remove `xunit` and `xunit.runner.visualstudio`
- Add `NUnit`, `NUnit3TestAdapter`, `NUnit.Analyzers`
- Remove `<Using Include="Xunit" />`
- Add `<Using Include="NUnit.Framework" />`

### 9. Split Unit and Integration Tests into Separate Projects

Create:
- `src/LuaHelperMcpServer.Tests.Unit/` — pure unit tests (no real processes, no filesystem)
- `src/LuaHelperMcpServer.Tests.Integration/` — integration tests (real lualsp.exe, real files)

Move:
- `LspMessageReaderTests.cs` → Unit
- `LspMessageWriterTests.cs` → Unit
- `DiagnosticCacheTests.cs` → Unit
- `LspClientTests.cs` → Unit (after mocking file I/O)
- `ProcessManagerTests.cs` → Integration (spawns real processes)
- `LspClientIntegrationTests.cs` → Integration

Update solution file to include both test projects.

### 10. Mirror Test File Structure to Source Structure

Tests must follow the same folder hierarchy as the source they test:

```
src/LuaHelperMcpServer.Tests.Unit/
├── Services/
│   ├── LspMessageReaderTests.cs
│   ├── LspMessageWriterTests.cs
│   ├── DiagnosticCacheTests.cs
│   └── LspClientTests.cs
├── Models/
│   └── (future model tests)
└── Helpers/
    ├── FakeLspServer.cs
    └── MockProcessManager.cs

src/LuaHelperMcpServer.Tests.Integration/
├── Services/
│   ├── ProcessManagerTests.cs
│   └── LspClientIntegrationTests.cs
└── Fixtures/
    ├── test_with_warning.lua
    └── test_clean.lua
```

### 11. Extract `PartialWriteStream` to Own File

**File:** `src/LuaHelperMcpServer.Tests.Unit/Helpers/PartialWriteStream.cs`

Move the nested `PartialWriteStream` class from `LspMessageReaderTests.cs` into its own file.

### 12. Mock File I/O in Unit Tests

**File:** `src/LuaHelperMcpServer.Tests.Unit/Services/LspClientTests.cs`

The current `LspClientTests` creates and deletes temp files (`Path.GetTempFileName()`). Unit tests must NOT touch the filesystem.

Options:
- **Option A:** Mock `File.ReadAllTextAsync` and `File.Exists` using Moq. This requires `LspClient` to accept a file reader abstraction (interface).
- **Option B:** Migrate these specific tests to the Integration project.

**Preferred:** Option A — introduce an `IFileReader` interface (or make `LspClient` accept file content as a parameter in tests). Use Moq framework.

Add Moq to the Unit test project:
```xml
<PackageReference Include="Moq" Version="4.20.72" />
```

### 13. Mock Process Spawning in Unit Tests

**File:** `src/LuaHelperMcpServer.Tests.Unit/Services/ProcessManagerTests.cs`

The current `ProcessManagerTests` spawns real `cmd.exe` processes. This belongs in integration tests.

**Action:** Move `ProcessManagerTests.cs` to the Integration project. In the Unit project, either:
- Remove the tests entirely (since they're integration tests), OR
- Create a mock-based version that verifies the `ProcessStartInfo` is constructed correctly without actually starting a process.

### 14. Add Meaningful Assertions to Tests

Replace shallow assertions with specific, meaningful ones:

**Before:**
```csharp
Assert.NotEmpty(warningDiags);
Assert.Empty(cleanDiags);
```

**After:**
```csharp
Assert.That(warningDiags, Has.Count.EqualTo(1));
Assert.That(warningDiags[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
Assert.That(warningDiags[0].Message, Does.Contain("Frame"));
Assert.That(warningDiags[0].StartLine, Is.EqualTo(0));
Assert.That(cleanDiags, Is.Empty);
```

Apply this pattern to ALL test assertions. Every test should verify specific values, not just "not empty" or "not null".

### 15. Integration Tests Must Install lualsp.exe Locally

**File:** `src/LuaHelperMcpServer.Tests.Integration/Services/LspClientIntegrationTests.cs`

Remove hardcoded paths:
```csharp
private const string LualspPath = @"C:\Users\dnmno\.vscode\extensions\yinfei.luahelper-0.2.29\server\lualsp.exe";
```

Instead, at test initialization:
1. Check if `lualsp.exe` exists in a local test directory (e.g., `TestAssets/lualsp/win-x64/lualsp.exe`).
2. If not, download or copy it from the known extension path (read from environment variable `LUAHELPER_EXTENSION_PATH` if set).
3. Use the local path for all tests.

This ensures tests work on any machine, not just the developer's.

---

## Medium Priority Fixes

### 16. Add `appsettings.json`

**File:** `src/LuaHelperMcpServer/appsettings.json`

Create with default configuration matching architecture doc section 9.

### 17. Add `LuaHelperOptions` Model

**File:** `src/LuaHelperMcpServer/Models/LuaHelperOptions.cs`

For `appsettings.json` binding (separate from `LuaHelperConfig` which is for `luahelper.json`).

### 18. Add `InitializeOptions` Model

**File:** `src/LuaHelperMcpServer/Models/InitializeOptions.cs`

Strongly-typed model for LSP `initializationOptions` instead of `Dictionary<string, object?>`.

### 19. Add `DiagnosticCollection` Model

**File:** `src/LuaHelperMcpServer/Models/DiagnosticCollection.cs`

Per architecture doc section 4 — aggregates diagnostics across files with `TotalCount`, `Timestamp`, `ToFormattedString()`.

### 20. Add `ServiceCollectionExtensions`

**File:** `src/LuaHelperMcpServer/Extensions/ServiceCollectionExtensions.cs`

DI registration helper for all services.

### 21. Fix `GetDiagnosticsAsync` to Open File If Not Already Open

**File:** `src/LuaHelperMcpServer/Services/LspClient.cs`

Current behavior: assumes file is already open. Fix: check cache, if not present, open the file first, then wait for diagnostics.

### 22. Add `_diagnosticChannel` Per Architecture

**File:** `src/LuaHelperMcpServer/Services/LspClient.cs`

Add `Channel<JsonElement> _diagnosticChannel` for backpressure-safe diagnostics routing (architecture doc section 8).

### 23. Format All Code with CSharpier

Install and run CSharpier on all `.cs` files:
```powershell
dotnet tool install -g csharpier
dotnet csharpier .
```

---

## Definition of Done

- [ ] All Critical issues fixed
- [ ] All High issues fixed
- [ ] All Medium issues fixed (or documented as deferred)
- [ ] `dotnet build` succeeds for all projects
- [ ] `dotnet test` passes for Unit project (no real processes, no filesystem)
- [ ] `dotnet test` passes for Integration project (real lualsp.exe, real files)
- [ ] All code formatted with CSharpier
- [ ] No method > 20 lines (exceptions documented)
- [ ] No nesting > 3 levels
- [ ] No "what" comments (only "why" comments remain)
- [ ] No empty catch blocks
- [ ] No hardcoded machine-specific paths
- [ ] Test structure mirrors source structure
- [ ] All assertions are specific and meaningful

## Report Format

After completing the refactoring, write a brief report:
- What was done (grouped by severity: Critical, High, Medium)
- Which tests pass
- DoD met / not met
- If not met — what remains and why
