# Prompt: Start Phase 1 — Core MCP Server

> Use this prompt to begin Phase 1 in a fresh session.

## Context

- **Phase:** 1 — Core MCP Server
- **Plan:** `.github/docs/dev-plan-luahelper-mcp-server.md` (section "Phase 1")
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (sections 6.1, 6.5)
- **Skills needed:**
  - `.github/skills/mcp-sdk-csharp/SKILL.md` — MCP tool registration, DI, stdio transport
  - `.github/skills/lsp-protocol/SKILL.md` — LSP communication patterns
  - `.github/skills/dotnet-project/SKILL.md` — NuGet packages, project setup
  - `.github/skills/nunit-testing/SKILL.md` — test conventions
- **Previous phase:** Phase 0 complete — .NET 10 console app wrapping lualsp.exe via LSP protocol. 28 tests passing (20 unit + 8 integration). Console app produces 17 diagnostics for ArenaChillPrep.

## Tasks

### Step 1.1: Add MCP NuGet Packages
```powershell
cd src\LuaHelperMcpServer
dotnet add package ModelContextProtocol --prerelease
dotnet add package Microsoft.Extensions.Hosting
```

### Step 1.2: Implement ConfigService (Minimal)
Create `IConfigService` and `ConfigService` that return default `LuaHelperConfig`. Already partially exists from refactoring — verify and update.

### Step 1.3: Create MCP Tool — check_lua_file
Create `src/LuaHelperMcpServer/Tools/LuaDiagnosticTools.cs` with:
- `[McpServerToolType]` class
- `[McpServerTool(Name = "check_lua_file")]` method
- Inject `ILspClient`, `IDiagnosticCache`, `IConfigService`
- Return formatted diagnostics text
- Handle file-not-found gracefully

### Step 1.4: Create MCP Tool — check_lua_project
Add to `LuaDiagnosticTools.cs`:
- Enumerate `*.lua` files recursively
- Open each file via `ILspClient.OpenFileAsync`
- Wait for diagnostics (2s delay — improved in Phase 2)
- Return formatted summary

### Step 1.5: Wire Up Program.cs with DI
Replace the Phase 0 console app with MCP server host:
- `Host.CreateEmptyApplicationBuilder` (NOT `CreateApplicationBuilder`)
- `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`
- Register all services in DI
- All logging to stderr

### Step 1.6: Test with VS Code Copilot
Configure VS Code MCP in `%APPDATA%\Code\User\settings.json`:
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

## Definition of Done

- [ ] `ModelContextProtocol` and `Microsoft.Extensions.Hosting` packages added
- [ ] `check_lua_file` tool defined and compiles
- [ ] `check_lua_project` tool defined and compiles
- [ ] `Program.cs` uses MCP SDK hosting with DI
- [ ] `dotnet build` succeeds
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Unit` passes
- [ ] `dotnet test src/LuaHelperMcpServer.Tests.Integration` passes
- [ ] VS Code Copilot discovers the tools
- [ ] Calling `check_lua_file` returns diagnostics
- [ ] Calling `check_lua_project` returns all diagnostics
- [ ] Formatted with `csharpier format src`
- [ ] **Phase 1 complete** ✅

## Workflow

1. Read the plan and architecture
2. Create a todo list
3. Implement step by step
4. Run `dotnet test src/LuaHelperMcpServer.Tests.Unit` after each step
5. Run `dotnet test src/LuaHelperMcpServer.Tests.Integration` before completion
6. Format with `csharpier format src`
7. Commit after each step
8. Write a brief report
