# Agent: luahelper-developer

Main agent for implementing phases, features, and bugfixes in the LuaHelper MCP Server.

## Startup ritual

1. Read `AGENTS.md` at repo root
2. Read `.github/CONTEXT.md` for project context
3. Read `.github/ARCHITECTURE.md` for architecture reference
4. Read the relevant skill(s) for the task:
   - LSP changes → `.github/skills/lsp-protocol/SKILL.md`
   - MCP tools → `.github/skills/mcp-sdk-csharp/SKILL.md`
   - lualsp.exe flags → `.github/skills/lualsp-exe/SKILL.md`
   - New projects/packages → `.github/skills/dotnet-project/SKILL.md`
   - Tests → `.github/skills/nunit-testing/SKILL.md`
   - Process management → `.github/skills/process-lifecycle/SKILL.md`
5. Read the dev plan: `.github/docs/dev-plan-luahelper-mcp-server.md`
6. Read the architecture doc: `.github/docs/arch-luahelper-mcp-server.md`

## Rules

- Follow everything in `.github/`. It is the source of truth.
- Contract-first: update `.github/` before code.
- Run unit tests after every change: `dotnet test src/LuaHelperMcpServer.Tests.Unit`
- Run integration tests before phase completion: `dotnet test src/LuaHelperMcpServer.Tests.Integration`
- Format with CSharpier before committing: `csharpier format src`
- Commit after each logical step — small, atomic commits.

## Output format

After completing a phase or feature:

```markdown
## {Phase/Feature} complete ✅

### What was done
- Step 1: ...
- Step 2: ...

### Test results
- Unit: X tests, 0 failures
- Integration: Y tests, 0 failures

### DoD met? ✅/❌
- [ ] Item 1
```
