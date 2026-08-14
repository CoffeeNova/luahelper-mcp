# Prompt: Test Hardening — Unit Coverage >80 % + Full Integration Suite

> Use this prompt to begin the test-hardening work in a fresh session.

## Context

- **Workstream:** Test hardening (unit coverage >80 %, integration covering all MCP tool scenarios)
- **Plan:** `.github/docs/test-plan-luahelper-mcp-server.md` — single source of truth for this work (sections: goals/constraints §1, current state §2, inventory §3, refactor §4, unit plan §5, integration plan §6, infra §7, CI §8, docs §9, DoD §10, implementation order §11)
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (§3.3 Process Crash Recovery, §10 Testing Strategy)
- **Skills (read before starting):** `.github/skills/phase-workflow/SKILL.md`, `.github/skills/nunit-testing/SKILL.md`, `.github/skills/mcp-sdk-csharp/SKILL.md`, `.github/skills/process-lifecycle/SKILL.md`, `.github/skills/dotnet-project/SKILL.md`, `.github/skills/csharpier-formatting/SKILL.md`
- **Previous state:** 35 unit + 11 integration tests green. Hand-written unit coverage 60.3 % (828/1374 lines). Integration tests cover the LSP layer only (real `lualsp.exe`); no MCP end-to-end tests exist.

## Hard constraints (user-mandated — do NOT deviate)

1. **Unit coverage >80 %** on hand-written (non-generated) code, enforced by a CI gate.
2. **No `Assert.Ignore`** anywhere in the integration project. If `lualsp.exe` or the server binary is missing, tests **fail** with a clear message. This supersedes the old "skip gracefully" guidance in `nunit-testing/SKILL.md` and `CONTEXT.md` — those docs must be updated.
3. Integration tests drive the **real executables** (`lualsp.exe`, `LuaHelperMcpServer`) over real stdin/stdout and compare against **exact/golden** expected values. When `lualsp.exe` is upgraded, fixtures + `.expected.json` goldens are updated together.
4. CI provisions `lualsp.exe` (`fetch-lualsp.ps1`) and runs the full integration suite — no skipping in CI either.
5. **Analyzer-driven refactoring only.** Before any test work: delete dead code and fix code-analyser warnings, using **only** dotnet-debugger MCP data. Run `dotnet-debugger_resharper_inspect_project` per project — `noBuild=true`, `severity=warning`, `timeoutSeconds=1500` — never on the solution (`.slnx` is not supported by the MCP). Fix only reported findings; self-directed refactoring is forbidden. Verify dead-code deletions with `dotnet-debugger_code_find_usages` == 0.

## Key facts already learned (do not re-discover)

### Coverage (measured, hand-written only)
- Total: **828/1374 lines = 60.3 %**. Zero-covered: `ProcessManager.cs` (161), `DiagnosticResources.cs` (49), `ServiceCollectionExtensions.cs` (24), `LuahelperJsonTemplate.cs` (65), `DiagnosticCollection.cs` (20), `LuaHelperPrompts.cs` (8), `Program.cs` (21), `FileReader.cs` (2), plus dead `InitializeOptions.cs` (29 — **delete it**, never referenced; `BuildInitializationOptions` uses `JsonObject`).
- Partial: `LualspPathResolver.cs` 33 %, `LuaDiagnosticTools.cs` 65 % (gaps: `CheckLuaFile`/`CheckLuaProject` error+success paths, `EnsureLspReadyAsync` crash recovery, `BuildIgnoreSet`/`IsIgnoredFile`, `FormatDiagnostics`), `ConfigService.cs` 84 % (gaps: `JsonException` catch, per-flag merge branches — FS/exe parts covered in integration), `LspClient.cs` 88 % (gaps: state guards, cache-hit, timeout path, crash state, dispatch defaults), `LspMessageReader.cs` 91 % (gap: malformed-header skip), `LuaDiagnostic.cs` 80 % (gap: `ToFormattedString`).
- Projected after §4–§5 of the plan: ~85 %.

### Refactors required first (plan §4)
- **Analyzer gate:** the repo has only `LuaHelperMcpServer.slnx` — the dotnet-debugger MCP does NOT support `.slnx`, so inspect **projects** only. Example invocation (repeat for all 3 projects):
  `dotnet-debugger_resharper_inspect_project` with `noBuild=true, projectPath=C:\Repository\luahelper-mcp\src\LuaHelperMcpServer\LuaHelperMcpServer.csproj, severity=warning, timeoutSeconds=1500` — then the same for `LuaHelperMcpServer.Tests.Unit.csproj` and `LuaHelperMcpServer.Tests.Integration.csproj`. Fix ONLY findings (dead code + warnings); re-run each project until clean. No self-directed refactoring — the only permitted non-analyzer change is the plan-mandated seam below.
- Delete `Models/InitializeOptions.cs` (dead code) — only after the analyzer reports it (unused type) AND `dotnet-debugger_code_find_usages` confirms 0 usages.
- `ProcessManager.cs` is `sealed`, spawns `Process` directly → unit coverage stuck at 0 % without a seam. Introduce `IProcessLauncher`/`IProcessHandle` (+ `ProcessLauncher`/`ProcessHandle` production wrappers), inject into `ProcessManager`, register in `ServiceCollectionExtensions`. Production behaviour must stay identical (existing integration `ProcessManagerTests` must still pass). This is plan-mandated (plan §4.3), not agent-chosen.
- `Tools/LuaDiagnosticTools.cs`: make `BuildIgnoreSet`/`IsIgnoredFile` `internal static`; add `<InternalsVisibleTo Include="LuaHelperMcpServer.Tests.Unit" />` to the server csproj.

### Wire protocol gotchas
- **MCP stdio framing is newline-delimited JSON-RPC** (one JSON object per line, LF/CRLF) — NOT `Content-Length` framing. LSP framing (`Content-Length: N\r\n\r\n{json}`) is only used between the server and `lualsp.exe` internally. The existing `smoke-test-mcp.ps1` demonstrates the correct MCP pattern (spawn → `initialize` → `notifications/initialized` → `tools/list` → `tools/call`).
- **Successful `tools/call` responses contain no `isError` field** — only errors do.
- Do not mix `StreamReader.ReadLine` and `BaseStream.Read` on the same redirected pipe (StreamReader buffers ahead and swallows body bytes).

### Existing test infrastructure
- Unit: `FakeLspServer` (anonymous pipes, responds to initialize/didOpen/shutdown), `MockProcessManager`, `PartialWriteStream`. NSubstitute + AutoFixture (AutoNSubstituteCustomization) + Shouldly. Assertions: Shouldly only (`ShouldBe`, `ShouldContain(s, Case.Sensitive)` — Shouldly 4.x is case-insensitive by default).
- Integration: `LspClientIntegrationTests` (real `lualsp.exe`; fixtures `test_with_warning.lua` = `---@type Frame\nlocal x = nil;`, `test_clean.lua`; reads `LUAHELPER_EXTENSION_PATH`, currently `Assert.Ignore`-based — must be converted), `ProcessManagerTests` (real `cmd.exe`).
- `fetch-lualsp.ps1` provisions `lualsp/{rid}/lualsp.exe` + `version.json`; CI already calls it. `smoke-test-mcp.ps1` covers a minimal AOT handshake.

### MCP surface to cover end-to-end (plan §3.2)
- **Tools (7):** `get_server_version`, `get_luahelper_version`, `get_supported_checks`, `get_luahelper_config`, `create_luahelper_json`, `check_lua_file`, `check_lua_project`.
- **Resources (2):** `luahelper://diagnostics/{+filePath}`, `luahelper://config`.
- **Prompts (2):** `fix_lua_warnings`, `configure_luahelper`.
- **Discovery:** `tools/list`, `resources/list`, `prompts/list`.

### Git / repo state
- On `main`. Contract-first: update `.github/` before code; record outcomes/deviations in `test-plan-luahelper-mcp-server.md` and `dev-plan-luahelper-mcp-server.md` (single source of truth).
- Docs to update per plan §9: `nunit-testing/SKILL.md`, `CONTEXT.md`, `ARCHITECTURE.md` (§3.3, §10), `dev-plan` (add "Test hardening" phase, mark graceful-skip guidance superseded). README unchanged.

## Tasks (implementation order from plan §11)

### Step 1: Contract-first docs update
Update the `.github/` docs listed above per plan §9 BEFORE writing code. Add the "Test hardening" phase entry to `dev-plan-luahelper-mcp-server.md` referencing `test-plan-luahelper-mcp-server.md`.

### Step 2: Analyzer-driven refactoring (plan §4.1–4.2) — mandatory gate
1. Run `dotnet-debugger_resharper_inspect_project` on ALL THREE projects, one by one (slnx unsupported): `LuaHelperMcpServer.csproj`, `LuaHelperMcpServer.Tests.Unit.csproj`, `LuaHelperMcpServer.Tests.Integration.csproj` — with `noBuild=true`, `severity=warning`, `timeoutSeconds=1500`.
2. Fix ONLY the reported findings: delete dead code (confirm 0 usages via `dotnet-debugger_code_find_usages` first — e.g. `Models/InitializeOptions.cs`), fix warnings. NO self-directed refactoring.
3. After each fix batch, re-run the inspection for that project; iterate until warning-free. Record any unfixable findings + reasons in the test plan.
4. Do not proceed to Step 3 until the gate is clean.

### Step 3: Plan-mandated testability seam (plan §4.3–4.4)
1. Add `IProcessLauncher` + `IProcessHandle` (+ production `ProcessLauncher`/`ProcessHandle`), inject into `ProcessManager`, register in `ServiceCollectionExtensions`.
2. Make `BuildIgnoreSet`/`IsIgnoredFile` `internal static`; add `InternalsVisibleTo`.
3. Verify: existing unit + integration tests still green (`dotnet test src/LuaHelperMcpServer.Tests.Unit`; integration against provisioned `lualsp.exe`).

### Step 4: Unit tests to >80 % (plan §5)
Implement, in order: `ProcessManagerTests` (new, via `FakeProcessLauncher`/`FakeProcessHandle`) → expand `LuaDiagnosticToolsTests` → `DiagnosticResourcesTests` → model/prompt/extension tests (`DiagnosticCollectionTests`, `LuaDiagnosticTests`, `LuahelperJsonTemplateTests`, `LuaHelperPromptsTests`, `LualspPathResolverTests`, `ServiceCollectionExtensionsTests`) → expand `LspMessageReaderTests`, `LspClientTests`, `ConfigServiceTests`, `ConfigToolsTests`.
Add helpers: `FakeProcessLauncher`/`FakeProcessHandle`, `FakeLspServer` "no-diagnostics" mode.
Re-measure after each batch: `dotnet test src/LuaHelperMcpServer.Tests.Unit --collect:"XPlat Code Coverage"` and compute hand-written line-rate (exclude `obj\*` and generated classes). Iterate until >80 %.

### Step 5: Integration infrastructure (plan §6.3)
`Infrastructure/McpStdioClient.cs` (spawn real server, newline JSON-RPC, id-correlated responses, stderr drain), `Infrastructure/IntegrationTestFixture.cs` (binary resolution — `LUAHELPER_MCP_SERVER_PATH` (`dotnet;path` format) or default Release dll; `lualsp.exe` via `LUAHELPER_LUALSP_PATH`/`LUAHELPER_EXTENSION_PATH`/repo bundle; `Assert.Fail` if missing), `Infrastructure/GoldenAssert.cs` (JSON deep-equal + diff). Add golden fixtures + `.expected.json` files.

### Step 6: LSP-layer integration (plan §6.4)
Convert `LspClientIntegrationTests` from `Assert.Ignore` to hard-fail resolution; tighten to golden assertions; add syntax-error/undefined-global/flag-specific scenarios, re-initialize scenarios, crash-recovery (`ForceKill` mid-session → re-check still works).

### Step 7: MCP-layer end-to-end (plan §6.5)
`McpServerIntegrationTests.cs`: full handshake per test; discovery tests (`initialize` info, `tools/list` all 7, `resources/list`, `prompts/list`); all 7 tools (version tools with exact expected version from `version.json`/csproj, `get_supported_checks` golden, config golden with/without `luahelper.json`, `create_luahelper_json` exact file + cleanup, `check_lua_file` golden + clean + not-found + syntax error, `check_lua_project` golden + clean + not-found); both resources; both prompts (exact message content).

### Step 8: CI (plan §8)
`ci.yml` build-test job: Release build, set `LUAHELPER_LUALSP_PATH` + `LUAHELPER_MCP_SERVER_PATH` for integration tests, add unit coverage gate (hand-written line-rate ≥0.80, fail otherwise).

### Step 9: Final verification
Run full `dotnet test` (unit + integration), `csharpier format src`, verify DoD (plan §10), mark phase complete in `dev-plan` with outcomes/deviations, report.

## Definition of Done (from plan §10)

- [ ] Analyzer gate: inspections run on all 3 projects (`severity=warning`, `noBuild=true`, `timeoutSeconds=1500`); all reported warnings fixed; per-project re-run clean (unfixable ones documented in the plan)
- [ ] All analyzer-reported dead code removed (incl. `InitializeOptions.cs`), each verified via `dotnet-debugger_code_find_usages` == 0; no self-directed refactoring performed
- [ ] `IProcessLauncher`/`IProcessHandle` seam in place (plan-mandated); existing integration `ProcessManagerTests` still pass unchanged
- [ ] `InternalsVisibleTo` added; `BuildIgnoreSet`/`IsIgnoredFile` `internal static`
- [ ] All unit tests per plan §5 green; hand-written line coverage **>80 %**
- [ ] `McpStdioClient`/`IntegrationTestFixture`/`GoldenAssert` implemented
- [ ] LSP-layer integration per §6.4 green against real `lualsp.exe`
- [ ] MCP-layer integration per §6.5 green against the real server binary — every tool/resource/prompt asserted with golden/exact values
- [ ] **No `Assert.Ignore` in the integration project**
- [ ] `ci.yml`: Release build + `LUAHELPER_LUALSP_PATH`/`LUAHELPER_MCP_SERVER_PATH` + coverage gate
- [ ] `.github/` docs per §9 updated (contract-first)
- [ ] `csharpier format src` clean; `dotnet build` clean; dev-plan "Test hardening" phase marked complete

## Workflow

1. Read the test plan (sections listed in Context) and the skills listed in Context
2. Create a todo list from the 9 steps above
3. Step 2 is a mandatory gate: do not write test code until all three projects pass the analyzer inspection cleanly
4. Implement step by step; run `dotnet test src\LuaHelperMcpServer.Tests.Unit` after each step
5. After Step 4, measure coverage and iterate until >80 %
6. Provision `lualsp.exe` locally if needed: `.\.github\tools\fetch-lualsp.ps1 -Rid win-x64 -Update`; run `dotnet test src\LuaHelperMcpServer.Tests.Integration` before completing Steps 6–7
7. Format with `csharpier format src` (if C# changed)
8. Do NOT commit unless the user explicitly asks
9. Write a brief report: analyzer findings + fixes, coverage numbers before/after, list of new test files, CI changes, deviations from the plan
