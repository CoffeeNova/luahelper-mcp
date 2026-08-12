# Prompt: Start Phase 2 — Full Tool Set

> Use this prompt to begin Phase 2 in a fresh session.

## Context

- **Phase:** 2 — Full Tool Set
- **Plan:** `.github/docs/dev-plan-luahelper-mcp-server.md` (section "Phase 2")
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (sections 6.5, 6.6)
- **Research:** `.github/docs/research-luahelper-mcp-server.md` (section 4, "Warning Types" table = the 22 check types)
- **Skills needed:**
  - `.github/skills/mcp-sdk-csharp/SKILL.md` — MCP tool/resource/prompt registration, DI, stdio transport
  - `.github/skills/lsp-protocol/SKILL.md` — LSP communication patterns
  - `.github/skills/nunit-testing/SKILL.md` — test conventions
  - `.github/skills/csharpier-formatting/SKILL.md` — formatting + `string.Empty` convention
- **Previous phase:** Phase 1 complete — MCP server hosting `check_lua_file` and `check_lua_project` over stdio. Verified end-to-end with a raw MCP handshake. 28 tests passing (20 unit + 8 integration). Note: Phase 1 work is currently **uncommitted** (last commit was undone) — do not re-commit it without being asked.

## Key facts already learned (do not re-discover)

- **MCP stdio framing is newline-delimited JSON-RPC** (one JSON message per line, LF or CRLF) — NOT `Content-Length` framing like LSP. The SDK handles it internally; never write to stdout.
- `Program.cs` must use `Host.CreateEmptyApplicationBuilder(settings: null)` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`.
- Tool classes are decorated with `[McpServerToolType]`, methods with `[McpServerTool(Name = "...")]`, parameters with `[Description]`. All logging via `ILogger<T>` to stderr.
- Use `string.Empty`, never the `""` literal.
- `LspClient.GetDiagnosticsAsync` already uses a TCS-based wait (`_pendingDiagnostics`) with a 10s timeout — verify before re-implementing.
- `check_lua_project` currently uses `Task.Delay(2000)` — this is the hack to remove in this phase.

## Tasks

### Step 2.1: Add Remaining Tools
**Add to** `src/LuaHelperMcpServer/Tools/LuaDiagnosticTools.cs`:
1. `get_supported_checks` — returns all 22 check types as JSON (hardcoded array; see research doc section 4 table)
2. `get_luahelper_version` — returns version string

**Create** `src/LuaHelperMcpServer/Tools/ConfigTools.cs` (see arch doc section 6.6):
3. `get_luahelper_config` — returns current config as JSON (`IConfigService.GetConfig`)
4. `create_luahelper_json` — creates default `luahelper.json` in project root (`IConfigService.CreateDefaultConfigAsync`)

### Step 2.2: Improve Diagnostic Wait Logic
- **Update** `LspClient.GetDiagnosticsAsync`: the TCS-based wait with 10s timeout already exists — verify it resolves on `publishDiagnostics` and returns cached diagnostics (or empty list with warning) on timeout. Only fix gaps.
- **Update** `check_lua_project`: replace `Task.Delay(2000)` with a wait for all opened files' diagnostics, with a global 30s timeout. Consider adding a method to `ILspClient`/`IDiagnosticCache` to track pending files, or wait per-file via `GetDiagnosticsAsync` (which already waits properly).

### Step 2.3: Add MCP Resources
**Create** `src/LuaHelperMcpServer/Resources/DiagnosticResources.cs`:
1. `luahelper://diagnostics/{filePath}` — diagnostics for a specific file as JSON
2. `luahelper://config` — current config as JSON

**Note:** Check the C# SDK docs for the exact resource registration API — it may use `[McpServerResource]` attributes or handler-based registration via `McpServerOptions`. Do NOT assume; verify against the SDK.

### Step 2.4: Add MCP Prompts
**Create** `src/LuaHelperMcpServer/Prompts/LuaHelperPrompts.cs`:
1. `fix_lua_warnings` — template: "Analyze the Lua file at {filePath} and suggest fixes for all warnings reported by LuaHelper."
2. `configure_luahelper` — template: "Help me configure luahelper.json for my Lua project at {projectPath}. Consider the WoW API globals that should be ignored."

## Definition of Done

- [ ] All 6 tools defined
- [ ] `tools/list` returns all 6 tools
- [ ] Each tool returns correct output when called (verify via raw stdio handshake)
- [ ] No more `Task.Delay` hacks in `check_lua_project`
- [ ] `GetDiagnosticsAsync` waits for `publishDiagnostics` with 10s timeout
- [ ] `check_lua_project` waits for all files with 30s global timeout
- [ ] `resources/list` returns the 2 resources
- [ ] `resources/read` returns content for each resource
- [ ] `prompts/list` returns the 2 prompts
- [ ] `prompts/get` returns the prompt text
- [ ] `dotnet build` succeeds
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Unit` passes
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Integration` passes (set `LUAHELPER_EXTENSION_PATH` if needed)
- [ ] Formatted with `csharpier format src`
- [ ] **Phase 2 complete** ✅

## Workflow

1. Read the plan and architecture
2. Create a todo list
3. Implement step by step
4. Run `dotnet test src/LuaHelperMcpServer.Tests.Unit` after each step
5. Run `dotnet test src/LuaHelperMcpServer.Tests.Integration` before completion
6. Format with `csharpier format src`
7. Do NOT commit unless the user explicitly asks — the previous commit was undone by the user
8. Write a brief report