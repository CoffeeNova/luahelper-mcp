# Prompt: Start Phase 4 — lualsp Provisioning + VS Code Extension

> Paste this prompt into a fresh session to begin Phase 4.

## Context

- **Phase:** 4 — lualsp Provisioning + VS Code Extension
- **Plan:** `.github/docs/dev-plan-luahelper-mcp-server.md` (section "Phase 4: lualsp Provisioning + VS Code Extension")
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (section 11 Phase 4, section 12 project structure, section 13 Q1)
- **Skills:** read `phase-workflow` before starting; `dotnet-project` for any scaffolding; `lualsp-exe` for lualsp.exe details (server binary location inside the extension, `-mode=1`)
- **Previous phase:** Phase 3 completed — full `luahelper.json` support: ConfigService loads/merges `appsettings.json` + `luahelper.json`, maps fields to `initializationOptions`, `LuaHelperOptions` bound from config, `ProcessManager` uses `LualspPath` from options
- **Key constraint:** this machine may NOT have the LuaHelper VS Code extension installed. Do not assume `lualsp.exe` exists at `%USERPROFILE%\.vscode\extensions\yinfei.luahelper-*\server\lualsp.exe` — the provisioning script must detect, download, and bundle it.

## Tasks

1. **Step 4.1** — Create `.github/tools/fetch-lualsp.ps1`:
   - Detect every installed lualsp version (`.vscode`, `.vscode-insiders`, `.cursor` extension dirs; version from folder name or `package.json`)
   - Check the local bundle `lualsp/{Rid}/lualsp.exe` + `lualsp/version.json`
   - Compare; download when missing (Marketplace VSIX via `https://marketplace.visualstudio.com/_apis/public/gallery/publishers/yinfei/vsextensions/luahelper/latest/vspackage`, fallback `code --install-extension`), offer update interactively (or `-Update` silently)
   - Form the bundle `lualsp/{Rid}/lualsp.exe` + `version.json` manifest (version, rid, source, sha256, fetchedAt)
   - Parameters: `-Rid` (win-x64), `-OutputDir` (lualsp), `-Update`, `-Force`, `-SkipDownload`
   - Idempotent: up-to-date bundle → status + exit 0, no changes
2. **Step 4.2** — Run `.\ .github\tools\fetch-lualsp.ps1`, verify bundle launches (`dotnet run --project src\LuaHelperMcpServer -- "E:\Repository\ArenaChillPrep"` produces diagnostics)
3. **Step 4.3** — Create `vscode-extension/package.json` with `contributes.mcpServerDefinitionProviders` declaration (id `luahelper`; note: there is NO `contributes.mcpServers` contribution point in VS Code — that key is silently ignored)
4. **Step 4.4** — Create `vscode-extension/extension.ts` + `tsconfig.json` — register the MCP server via `vscode.lm.registerMcpServerDefinitionProvider('luahelper', ...)` returning an `McpStdioServerDefinition` pointing at `${extensionPath}/LuaHelperMcpServer.exe`
5. **Step 4.5** — Create `vscode-extension/.vscodeignore` and build script `.github/tools/build-vsix.ps1` (calls `fetch-lualsp.ps1 -Rid win-x64 -Update` first, then `dotnet publish` AOT, copy lualsp, `vsce package`)
6. **Step 4.6** — Test: install `.vsix` via `code --install-extension`, verify Copilot can call `check_lua_file`

## Definition of Done

- [ ] Script lists every lualsp version found on the machine (including zero)
- [ ] Script downloads lualsp from the Marketplace VSIX when none is found
- [ ] Script offers to update when a newer version is found than the bundle
- [ ] Script forms `lualsp/{Rid}/lualsp.exe` + `lualsp/version.json`
- [ ] Script is idempotent — second run with an up-to-date bundle changes nothing
- [ ] `lualsp/win-x64/lualsp.exe` exists and launches (produces diagnostics)
- [ ] `lualsp/version.json` matches the actual binary version
- [ ] `vscode-extension/package.json` is valid; `contributes.mcpServerDefinitionProviders` declares the provider
- [ ] Extension compiles with `npm run compile`; no runtime errors on activation
- [ ] `build-vsix.ps1` produces a `.vsix` containing the .NET binary + lualsp.exe; succeeds on a machine with no LuaHelper extension installed
- [ ] Extension installs without errors; Copilot can use `check_lua_file` after install
- [ ] **Phase 4 complete** ✅

## Workflow

1. Read the plan and architecture docs (sections listed in Context)
2. Read the `phase-workflow` skill and follow it (contract-first: update `.github/` before code)
3. Create a todo list from the 6 steps
4. Implement step by step; run `.\.github\tools\fetch-lualsp.ps1 -SkipDownload` and `dotnet test src\LuaHelperMcpServer.Tests.Unit` after each step to confirm nothing broke
5. Run `dotnet test src\LuaHelperMcpServer.Tests.Integration` before completion
6. Format with `csharpier format src` (if C# files changed)
7. Write a brief report (what was created, script behavior on this machine, bundle version fetched, `.vsix` result)