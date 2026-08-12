# Prompt: Start Phase 3 — Configuration

> Use this prompt to begin Phase 3 in a fresh session.

## Context

- **Phase:** 3 — Configuration
- **Plan:** `.github/docs/dev-plan-luahelper-mcp-server.md` (section "Phase 3")
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (sections 6.6 ConfigTools, 9 Configuration)
- **Research:** `.github/docs/research-luahelper-mcp-server.md` (section 4 — `luahelper.json` ↔ `initializationOptions` mapping)
- **Skills needed:**
  - `.github/skills/mcp-sdk-csharp/SKILL.md` — MCP tool/resource/prompt registration, DI, stdio transport
  - `.github/skills/lsp-protocol/SKILL.md` — `initializationOptions` shape expected by lualsp.exe
  - `.github/skills/nunit-testing/SKILL.md` — test conventions (unit tests must not touch filesystem)
  - `.github/skills/csharpier-formatting/SKILL.md` — formatting + `string.Empty` convention
- **Previous phase:** Phase 2 complete — 6 tools (`check_lua_file`, `check_lua_project`, `get_supported_checks`, `get_luahelper_version`, `get_luahelper_config`, `create_luahelper_json`), 2 resources (`luahelper://config`, `luahelper://diagnostics/{+filePath}`), 2 prompts (`fix_lua_warnings`, `configure_luahelper`). Verified end-to-end with a raw MCP stdio handshake. 28 unit + 8 integration tests passing. Work is committed.

## Key facts already learned (do not re-discover)

- **MCP stdio framing is newline-delimited JSON-RPC** (one JSON message per line) — NOT `Content-Length` framing like LSP. The SDK handles it internally; never write to stdout.
- `Program.cs` uses `Host.CreateEmptyApplicationBuilder(settings: null)` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly().WithResourcesFromAssembly().WithPromptsFromAssembly()`.
- Tool classes: `[McpServerToolType]` / `[McpServerTool(Name = "...")]` / `[Description]`. All logging via `ILogger<T>` to stderr. Use `string.Empty`, never the `""` literal. Never leave a blank catch block — always log the exception.
- **`ConfigService`** constructor is `(string lualspPath, ILogger<ConfigService> logger)`. `GetConfig(projectPath)` currently returns **hardcoded defaults** — it does NOT load `appsettings.json` or `{projectPath}/luahelper.json` yet. This is the main gap for Step 3.1.
- `ConfigService.CreateDefaultConfigAsync(projectPath)` **already writes** the default `luahelper.json` template with WoW `IgnoreModules` (matches plan Step 3.1's template) — verify, only fill gaps.
- `LspClient.BuildInitializationOptions(config)` (in `LspClient.cs`) **already maps** all 22 check flags + `PluginPath` + `IgnoreFileOrDir` + `IgnoreFileOrDirError` + `RequirePathSeparator` + `client` into `initializationOptions`. Step 3.2's mapping is largely done — the gap is only that `GetConfig` feeds it hardcoded values.
- `Models/LuaHelperOptions.cs` + `CheckDefaults` **already exist** with all 22 fields (see `Models/`). `appsettings.json` **already exists** at `src/LuaHelperMcpServer/appsettings.json` with the exact arch-doc section 9 content.
- Gap in Step 3.3: `Program.cs` still reads `LUAHELPER_LUALSP_PATH` env var directly and does NOT call `builder.Services.Configure<LuaHelperOptions>(builder.Configuration.GetSection("LuaHelper"))`; `ProcessManager`/`ConfigService` do not consume `IOptions<LuaHelperOptions>`.
- **21 check types** exist (research doc section 4 lists 21, not 22); `get_supported_checks` returns those 21. `CheckDefaults`/`LuaHelperConfig` mirror them (1:1 names).
- `IFileReader` exists (`FileExists`, `ReadAllTextAsync`) and is DI-registered (`AddSingleton<IFileReader, FileReader>`) — use it in `ConfigService` so loading `luahelper.json` is mockable in unit tests (unit tests must not touch the filesystem).
- Existing tests: `ConfigServiceTests` (GetConfig_ReturnsDefaultsWithPluginPath, GetVersion_NoFile_FallsBackToDefault), `ConfigToolsTests`, `LuaDiagnosticToolsTests`, `LspClientTests`. 28 unit + 8 integration tests currently green.

## Tasks

### Step 3.1: Implement luahelper.json Loading
**Update:** `ConfigService.cs`

1. `GetConfig(projectPath)`:
   - Load defaults from `appsettings.json` via `IOptions<LuaHelperOptions>` (inject it)
   - Check if `{projectPath}/luahelper.json` exists (via `IFileReader.FileExists`)
   - If yes, parse it and **merge**: `luahelper.json` fields override defaults
   - Set `PluginPath` to the directory of `lualspPath`
   - Return merged `LuaHelperConfig`
2. `CreateDefaultConfigAsync(projectPath)` — verify the existing template already matches plan Step 3.1; only fix gaps (e.g., write via `IFileReader`-style abstraction or keep `File.WriteAllTextAsync` if already tested).

**DoD:**
- [ ] `luahelper.json` is loaded and merged with defaults
- [ ] `IgnoreModules` are passed to lualsp.exe as part of `initializationOptions`
- [ ] `create_luahelper_json` tool creates a valid config file
- [ ] Unit tests cover: no `luahelper.json` → defaults; `luahelper.json` overrides defaults

### Step 3.2: Map luahelper.json to initializationOptions
**Verify only:** `LspClient.BuildInitializationOptions` already maps the fields. Confirm every `luahelper.json` field from the plan table is honored (`IgnoreModules` → `IgnoreModules`, `IgnoreFileOrFloder` → `IgnoreFileOrDir`, `IgnoreFileErr` → `IgnoreFileOrDirError`, `PathSeparator` → `RequirePathSeparator`, `ShowWarnFlag` → `AllEnable`). Add any missing mapping. Note: `LuaHelperConfig` currently lacks `IgnoreModules` / `IgnoreFileVars` / `IgnoreErrorTypes` fields — decide whether to add them to `LuaHelperConfig` (and `initializationOptions`) to satisfy the plan DoD.

**DoD:**
- [ ] All `luahelper.json` fields are correctly mapped
- [ ] Integration test: WoW addon with `luahelper.json` produces no false positives for ignored modules

### Step 3.3: Add appsettings.json Configuration Binding
**Update:** `Program.cs`, `ProcessManager`, `ConfigService`

1. `appsettings.json` and `Models/LuaHelperOptions.cs` already exist — wire them up:
   - `builder.Services.Configure<LuaHelperOptions>(builder.Configuration.GetSection("LuaHelper"))`
   - Replace the direct `LUAHELPER_LUALSP_PATH` env-var read with options (`LuaHelperOptions.LualspPath`), keeping the env var as an override if desired
2. `ProcessManager` should take `LualspPath` (and restart/timeout settings) from `IOptions<LuaHelperOptions>` instead of a raw string, or have the DI registration resolve it from options.

**DoD:**
- [ ] `LuaHelperOptions` binds from configuration
- [ ] `ProcessManager` uses `LualspPath` from options
- [ ] `dotnet build` succeeds
- [ ] **Phase 3 complete** ✅

## Definition of Done

- [ ] `luahelper.json` is loaded and merged with defaults
- [ ] `IgnoreModules` are passed to lualsp.exe as part of `initializationOptions`
- [ ] `create_luahelper_json` tool creates a valid config file
- [ ] All `luahelper.json` fields are correctly mapped
- [ ] Integration test: WoW addon with `luahelper.json` produces no false positives for ignored modules
- [ ] `LuaHelperOptions` binds from configuration
- [ ] `ProcessManager` uses `LualspPath` from options
- [ ] `dotnet build` succeeds
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Unit` passes
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Integration` passes (set `LUAHELPER_EXTENSION_PATH` if needed)
- [ ] Formatted with `csharpier format src`
- [ ] **Phase 3 complete** ✅

## Workflow

1. Read the plan and architecture
2. Create a todo list
3. Implement step by step
4. Run `dotnet test src/LuaHelperMcpServer.Tests.Unit` after each step
5. Run `dotnet test src/LuaHelperMcpServer.Tests.Integration` before completion
6. Format with `csharpier format src`
7. Do NOT commit unless the user explicitly asks
8. Write a brief report