# Prompt: Start Phase 5 — NativeAOT + Distribution

> Use this prompt to begin Phase 5 in a fresh session.

## Context

- **Phase:** 5 — NativeAOT + Distribution
- **Plan:** `.github/docs/dev-plan-luahelper-mcp-server.md` (section "Phase 5: NativeAOT + Distribution", steps 5.1–5.4)
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (section 11 Phase 5, section 12 project structure — `.github/workflows/ci.yml` + `release.yml` are planned)
- **Skills:** read `.github/skills/phase-workflow/SKILL.md` before starting; `.github/skills/dotnet-project/SKILL.md` (csproj/publish), `.github/skills/mcp-sdk-csharp/SKILL.md` (AOT-safe `WithTools<T>` registration), `.github/skills/csharpier-formatting/SKILL.md` (format with `csharpier format src`), `.github/skills/nunit-testing/SKILL.md` (test conventions)
- **Previous phase:** Phase 4 complete and committed to `main` — `.github/tools/fetch-lualsp.ps1` provisions `lualsp/win-x64/lualsp.exe` v0.2.29 (+`version.json`, sha256 `d4b9f67b…`), `vscode-extension/` wraps the MCP server (`contributes.mcpServerDefinitionProviders` + `registerMcpServerDefinitionProvider`), `.github/tools/build-vsix.ps1` packages `luahelper-mcp-0.1.0.vsix` (42 MB), extension installs and passes a full MCP handshake. 32 unit + 10 integration tests green. Repo root README does NOT exist yet; `.github/workflows/` does not exist yet.

## Key facts already learned (do not re-discover)

### AOT on this machine
- **`dotnet publish -r win-x64 -p:PublishAot=true` FAILS here:** `error : Platform linker not found` (Microsoft.NETCore.Native.Windows.targets) — the machine has no Visual Studio C++ workload (no `link.exe`). Decide: install VS Build Tools (C++ workload, ~2–6 GB), or rely on GitHub-hosted runners (`windows-latest` has the linker), or keep the self-contained fallback in `build-vsix.ps1`/`deploy.ps1`.
- AOT publish currently emits **IL2026/IL3050 warnings** that must be fixed before AOT is usable. All reflection-based JSON and registration sites:
  - `Program.cs:23` — `WithToolsFromAssembly` / `WithResourcesFromAssembly` / `WithPromptsFromAssembly` → replace with **generic** `WithTools<T>` / `WithResources<T>` / `WithPrompts<T>` (AOT-safe, no reflection)
  - `System.Text.Json` without source generation: `LspMessageReader.cs:80` (Deserialize), `LspMessageWriter.cs:58` (SerializeToUtf8Bytes), `LuaDiagnosticTools.cs:227`, `ConfigTools.cs:37`, `DiagnosticResources.cs:49/56/71`, `ConfigService.cs:133`
  - Fix: add `[JsonSerializable(...)]` partial `JsonSerializerContext` classes and use `JsonSerializer.Serialize(x, Context.Default.X)` overloads
- `deploy.ps1` (`.github/tools/deploy.ps1`) already does an AOT publish + copies `lualsp/{rid}/` — verify it works once AOT is fixed.

### Provisioning / bundles
- Bundle is **win-x64 only** right now. `fetch-lualsp.ps1 -Rid linux-x64` / `-Rid osx-x64` exists but is **not platform-aware**: the binary search (`Get-ChildItem -Filter $exeName`) may pick the wrong platform's `lualsp` from the VSIX (the extension ships `server/bin/Windows|Darwin|Linux/lualsp`). Phase 5 must verify what the Marketplace VSIX actually contains per platform and make the selection Rid-aware before cross-platform CI is real.
- The bundle is gitignored (`lualsp/`); CI must call `fetch-lualsp.ps1 -Update` (headless) before publishing/tests.

### Extension packaging
- `vscode-extension/package.json` still has placeholder `"publisher": "your-publisher-id"` and **no `repository` field** — both must be real before Marketplace publish (`vsce` needs `--allow-missing-repository` meanwhile; `npx --yes @vscode/vsce` is used).
- `vsce package` warns: **LICENSE missing in the extension folder** (root `LICENSE` is MIT-only; per arch doc Q1 the repo needs a **BSD-3-Clause notice for lualsp.exe** added — the MIT license alone is not enough).
- Engines `^1.101.0` (`mcpServerDefinitionProviders` contribution point + `registerMcpServerDefinitionProvider` API; the old `contributes.mcpServers` key is ignored by VS Code), `"activationEvents": []` — keep.

### Testing / verification patterns
- Integration tests read `LUAHELPER_EXTENSION_PATH` (expects `{path}/server/lualsp.exe`); run them against a temp dir containing a copy of the bundled binary (proven pattern in Phase 4) or set the env var.
- Full MCP handshake script pattern (spawn exe → `initialize` → `notifications/initialized` → `tools/list` → `tools/call`) works for verifying a published binary; note: **successful `tools/call` responses contain no `isError` field** (only errors do).
- Do not mix `StreamReader.ReadLine` and `BaseStream.Read` on the same redirected pipe (StreamReader buffers ahead and swallows body bytes).

### Git / repo state
- On `main` (default branch, pushed). Stale `master` branch still exists locally + on `origin/master` — safe to delete, but ask first.
- GitHub credentials were re-authenticated as **CoffeeNova** during Phase 4 (cached `isalzh` token was dead and removed) — pushing works now.
- `.github/CONTEXT.md` + `.github/ARCHITECTURE.md` are the agent-facing context; contract-first: update `.github/` before code, record outcomes in `dev-plan` (no separate notes dir).

## Tasks

### Step 5.1: Enable NativeAOT
**Update:** `src/LuaHelperMcpServer/LuaHelperMcpServer.csproj`

1. Add `<PublishAot>true</PublishAot>` and `<InvariantGlobalization>true</InvariantGlobalization>`
2. Fix all IL2026/IL3050 warnings (generic `WithTools<T>` etc. + `JsonSerializerContext` source generation) so an AOT publish is **warning-free**
3. Resolve the linker prerequisite: install VS C++ workload on this machine, or verify on a CI runner — the DoD requires a working AOT binary; keep the self-contained fallback in `build-vsix.ps1` until AOT actually succeeds here
4. Test: `dotnet publish src\LuaHelperMcpServer -c Release -r win-x64 --self-contained -p:PublishAot=true -o publish\win-x64` → single `.exe` (~15 MB); verify it answers an MCP handshake (`tools/list` → `check_lua_file` present)

**DoD:**
- [ ] AOT compilation succeeds with no IL2026/IL3050 warnings
- [ ] Single `.exe` produced (~15 MB)
- [ ] AOT binary works with an MCP client (handshake test)

### Step 5.2: Cross-Platform Build + CI
**Create:** `.github/workflows/release.yml` (and `ci.yml` per arch doc section 12: build + test on push)

1. `ci.yml`: checkout → `dotnet build` → unit tests → integration tests (provision lualsp via `fetch-lualsp.ps1 -Update`, set `LUAHELPER_EXTENSION_PATH`)
2. `release.yml`: on tag `v0.1.0`, matrix over `win-x64`, `linux-x64`, `osx-x64`:
   - `dotnet publish -r {rid} -p:PublishAot=true` (+ `-p:InvariantGlobalization=true`)
   - Copy the platform `lualsp` binary (make `fetch-lualsp.ps1` Rid-aware first — see Key facts)
   - Upload artifacts (zip per platform + `.vsix` from `build-vsix.ps1`)
3. Note: Linux/macOS lualsp binaries must come from the extension VSIX or source — verify availability before promising those rids in CI

**DoD:**
- [ ] CI builds the `win-x64` AOT binary and uploads it as an artifact
- [ ] Workflow files follow arch doc section 12 names (`ci.yml`, `release.yml`)

### Step 5.3: Write README
**Create:** `README.md` at repo root (currently absent)

Sections (per plan): 1) what it is, 2) quick start (3 steps: install, configure, use), 3) configuration reference (`appsettings.json` + `luahelper.json`), 4) available MCP tools (table: name, description, parameters), 5) development (build, test, contribute), 6) license (MIT + BSD-3-Clause notice for lualsp.exe). Also add the BSD-3-Clause notice file/extension.

**DoD:**
- [ ] README covers all 6 sections
- [ ] Quick start verified by following it step by step

### Step 5.4: Create GitHub Release
1. Tag `v0.1.0`, push tag
2. GitHub Actions produces release with `luahelper-mcp-server-{rid}.zip` (AOT binary + lualsp) and `luahelper-mcp-0.1.0.vsix`
3. Write release notes
4. If publishing to the VS Code Marketplace: replace the publisher placeholder + add `repository`, then `vsce publish` (needs PAT — ask the user for it)

**DoD:**
- [ ] GitHub release published with assets
- [ ] Assets downloadable
- [ ] **Phase 5 complete** ✅

## Definition of Done

- [ ] AOT compilation succeeds; single-file exe works with MCP clients
- [ ] IL2026/IL3050 warnings eliminated (source-gen JSON, generic WithTools/Resources/Prompts)
- [ ] CI builds win-x64 AOT binary, uploads artifact
- [ ] README covers all sections; quick start tested
- [ ] GitHub release with zip + vsix assets
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Unit` passes
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Integration` passes (lualsp provisioned)
- [ ] Formatted with `csharpier format src` (if C# changed)
- [ ] Dev plan Phase 5 marked complete with outcomes + deviations
- [ ] **Phase 5 complete** ✅

## Workflow

1. Read the plan and architecture (sections listed in Context)
2. Read the `phase-workflow` skill and follow it (contract-first: update `.github/` before code)
3. Create a todo list from the 4 steps
4. Implement step by step; run `dotnet test src\LuaHelperMcpServer.Tests.Unit` after each step to confirm nothing broke
5. Run `dotnet test src\LuaHelperMcpServer.Tests.Integration` before completion
6. Format with `csharpier format src` (if C# files changed)
7. Do NOT commit unless the user explicitly asks
8. Write a brief report (what was created, AOT status on this machine, CI results, release state)