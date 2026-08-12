# Code Audit: LuaHelper MCP Server — Phase 0

> **Date:** 2026-08-12
> **Scope:** All code in `src/LuaHelperMcpServer/` and `src/LuaHelperMcpServer.Tests/`
> **Against:** `arch-luahelper-mcp-server.md`, `dev-plan-luahelper-mcp-server.md`

---

## 1. Architecture Compliance

### 1.1 Missing Components (per architecture doc section 12)

| Component | Status | Priority |
|---|---|---|
| `appsettings.json` | ❌ Missing | High |
| `LuaHelperOptions` model | ❌ Missing | High |
| `InitializeOptions` model | ❌ Missing | Medium |
| `ConfigService` / `IConfigService` | ❌ Missing (Phase 3) | Low — planned |
| `ServiceCollectionExtensions` | ❌ Missing | Medium |
| `DiagnosticCollection` model | ❌ Missing | Medium |
| `Tools/` directory (MCP tools) | ❌ Missing (Phase 1) | Low — planned |
| `Extensions/` directory | ❌ Missing | Medium |

### 1.2 Deviation from Architecture

| Issue | Architecture Says | Code Does | Severity |
|---|---|---|---|
| LSP restart/backoff | `ProcessManager` has backoff schedule (2s→4s→8s, max 3) | No restart logic in `ProcessManager` | **High** |
| `_diagnosticChannel` | `Channel<JsonElement>` for backpressure-safe diagnostics | Uses `ConcurrentDictionary<string, TCS>` only | Medium |
| `_writeLock` | `SemaphoreSlim(1,1)` for serialized writes | Present in `LspMessageWriter`, not in `LspClient` | Low |
| State on normal shutdown | `ReadLoopAsync` should not set `Crashed` on graceful exit | Always sets `_state = Crashed` in `finally` | **High** |
| `GetDiagnosticsAsync` | Opens file if not already open | Does NOT open file — assumes it's already open | Medium |
| `Program.cs` config | Should use DI + `appsettings.json` | Hardcoded paths + inline config | **High** |

### 1.3 Phase 0 DoD Gaps

| DoD Item | Status |
|---|---|
| `dotnet build` succeeds | ✅ Pass |
| `LspMessageReader` parses Content-Length frames | ✅ Pass |
| `LspMessageWriter` serializes JSON-RPC | ✅ Pass |
| `ProcessManager` spawns lualsp.exe | ✅ Pass (no restart logic) |
| `LspClient.Initialize` sends initialize + initialized | ✅ Pass |
| `LspClient.OpenFile` sends didOpen | ✅ Pass |
| Diagnostics collection receives publishDiagnostics | ✅ Pass |
| Test against ArenaChillPrep produces same 17 diagnostics | ⚠️ Not verified |

---

## 2. Code Quality Review

### 2.1 Method Length Violations (>20 lines)

| File | Method | Lines | Target |
|---|---|---|---|
| `LspClient.cs` | `EnsureInitializedAsync` | ~80 | 10-15 |
| `LspClient.cs` | `HandlePublishDiagnostics` | ~40 | 10-15 |
| `LspClient.cs` | `ReadLoopAsync` | ~35 | 10-15 |
| `LspClient.cs` | `ShutdownAsync` | ~30 | 10-15 |
| `ProcessManager.cs` | `EnsureRunningAsync` | ~35 | 10-15 |
| `LspMessageReader.cs` | `TryParseMessage` | ~35 | 10-15 |
| `FakeLspServer.cs` | `RunAsync` | ~20 | borderline |

### 2.2 Nesting Depth Violations (>3 levels)

| File | Method | Max Depth | Issue |
|---|---|---|---|
| `LspClient.cs` | `EnsureInitializedAsync` | 4+ | initParams dictionary nesting |
| `LspClient.cs` | `HandlePublishDiagnostics` | 4 | TryGetProperty → EnumerateArray → foreach → property access |
| `LspClient.cs` | `ReadLoopAsync` | 4 | while → if → TryGetProperty → switch |
| `LspMessageReader.cs` | `TryParseMessage` | 3+ | for loop → if chain → recursive call |

### 2.3 Comment Quality

| File | Line | Issue |
|---|---|---|
| `LspClient.cs` | `catch { /* Ignore — process may already be gone */ }` | **Empty catch — must log** |
| `LspClient.cs` | `// Send LSP initialize request` | "What" comment — extract to method |
| `LspClient.cs` | `// Wait for response with 30s timeout` | "What" comment — extract to method |
| `LspClient.cs` | `// Send initialized notification` | "What" comment — extract to method |
| `LspClient.cs` | `// Check cache first` | "What" comment — extract to method |
| `LspClient.cs` | `// Store file content for crash recovery` | "What" comment — extract to method |
| `LspClient.cs` | `// Resolve any pending diagnostic waiters` | "What" comment — extract to method |
| `LspMessageReader.cs` | `// Try to find a complete message` | "What" comment |
| `LspMessageReader.cs` | `// Read more data from the stream` | "What" comment |
| `LspMessageReader.cs` | `// Stream ended` | "What" comment |
| `LspMessageReader.cs` | `// Need more data` | "What" comment (×2) |
| `LspMessageReader.cs` | `// Parse headers` | "What" comment |
| `LspMessageReader.cs` | `// Invalid header — skip to next` | "What" comment |
| `LspMessageReader.cs` | `// Check if we have the full body` | "What" comment |
| `LspMessageReader.cs` | `// Parse the JSON body` | "What" comment |
| `ProcessManager.cs` | `// Read stderr in background to avoid deadlocks` | "Why" comment — acceptable |
| `ProcessManager.cs` | `// Give the process a moment to start` | "What" comment |
| `ProcessManager.cs` | `// Expected on shutdown` | Acceptable |

### 2.4 Private Field Comments

No XML doc comments on private fields found — ✅ compliant.

### 2.5 Empty Catch Block

**`LspClient.cs` — `ShutdownAsync`:**
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
**Rule violation:** Never silently swallow exceptions. Must log at minimum:
```csharp
catch (Exception ex)
{
    _logger.LogDebug(ex, "Exit notification failed — process likely already exited");
}
```

---

## 3. Test Review

### 3.1 Framework & Structure

| Issue | Current | Required |
|---|---|---|
| Test framework | xUnit | **NUnit** |
| Project separation | Single test project | **Unit + Integration as separate projects** |
| Test file paths | `Unit/LspMessageReaderTests.cs` | `Unit/Services/LspMessageReaderTests.cs` (mirror src structure) |
| Mocking framework | Hand-rolled `FakeLspServer` / `MockProcessManager` | **Moq** |

### 3.2 LspClientIntegrationTests

| Issue | Severity |
|---|---|
| Hardcoded path `C:\Users\dnmno\.vscode\extensions\...` | **Critical** — breaks on any other machine |
| Should download/install lualsp.exe to local test directory at init | High |

### 3.3 LspClientTests (Unit)

| Issue | Severity |
|---|---|
| Creates temp files via `Path.GetTempFileName()` | **High** — unit tests must not touch filesystem |
| Deletes files in cleanup | High |
| Should mock file I/O or migrate to integration | High |
| Assertions are shallow (`Assert.NotEmpty`, `Assert.Empty`) | Medium |

### 3.4 LspMessageReaderTests

| Issue | Severity |
|---|---|
| Contains nested `PartialWriteStream` class | **High** — must be in own file |

### 3.5 ProcessManagerTests

| Issue | Severity |
|---|---|
| Spawns real `cmd.exe` processes | **High** — belongs in integration tests, or mock |

### 3.6 Assertion Quality

Current assertions are mostly structural (`NotEmpty`, `Empty`, `NotNull`). They should verify:
- Specific diagnostic messages
- Line/column numbers
- Severity values
- Warning types
- Exact counts where applicable

---

## 4. Formatting

- Code is NOT formatted with CSharpier.
- Inconsistent brace styles (some on same line, some on new line).
- Trailing commas in enums (`LspState`, `DiagnosticSeverity`).

---

## 5. Summary of Required Actions

### Critical (must fix)
1. **Empty catch block** in `LspClient.ShutdownAsync` — log the exception
2. **Hardcoded paths** in `Program.cs` and `LspClientIntegrationTests` — use config
3. **State bug**: `ReadLoopAsync` sets `Crashed` on graceful shutdown
4. **Missing restart/backoff** logic in `ProcessManager`

### High Priority
5. Extract long methods into smaller ones (target 10-15 lines)
6. Reduce nesting depth to ≤3
7. Replace "what" comments with well-named methods
8. Migrate tests to NUnit
9. Split Unit/Integration into separate projects
10. Mirror test file structure to source structure
11. Extract `PartialWriteStream` to own file
12. Mock file I/O in unit tests (or migrate to integration)
13. Mock process spawning in unit tests (or migrate to integration)
14. Add meaningful assertions to tests
15. Integration tests must install lualsp.exe locally, not reference hardcoded paths

### Medium Priority
16. Add `appsettings.json` with default config
17. Add `LuaHelperOptions` model
18. Add `InitializeOptions` model
19. Add `DiagnosticCollection` model
20. Add `ServiceCollectionExtensions`
21. Fix `GetDiagnosticsAsync` to open file if not already open
22. Add `_diagnosticChannel` per architecture
23. Format all code with CSharpier

### Low Priority (planned for future phases)
24. Implement `ConfigService` (Phase 3)
25. Implement MCP tools (Phase 1)
26. Implement resources and prompts (Phase 2)
