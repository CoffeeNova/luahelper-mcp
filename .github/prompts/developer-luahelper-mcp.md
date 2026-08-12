# Prompt: Developer — LuaHelper MCP Server

> Use this agent: `dotnet-senior-developer` (or any coding agent)
> Context:
>   - `.github\docs\arch-luahelper-mcp-server.md` — architecture
>   - `.github\docs\dev-plan-luahelper-mcp-server.md` — development plan
>   - `.github\docs\research-luahelper-mcp-server.md` — research

## Task

Implement the LuaHelper MCP Server — a .NET 10 application that wraps `lualsp.exe` (a Go LSP server for Lua) and exposes Lua code diagnostics to AI assistants via the MCP protocol.

## What to do

Follow `dev-plan-luahelper-mcp-server.md` **phase by phase, step by step**.

### Rules

1. **Don't skip ahead** — each phase depends on the previous one. Do not start Phase 1 until Phase 0 is complete.
2. **Check DoD** — after each step, verify the Definition of Done is met. If not, go back and finish it.
3. **Commit after each step** — small, atomic commits.
4. **Tests are mandatory** — unit tests for every component, integration tests for the LSP client.
5. **Reference the architecture** — if the plan says "see architecture doc section 6.4", open `arch-luahelper-mcp-server.md` and read that section.

### Project Structure

Everything is created under `e:\Repository\luahelper-mcp\`. Already exists:
```
.github/
  docs/
    research-luahelper-mcp-server.md
    arch-luahelper-mcp-server.md
    dev-plan-luahelper-mcp-server.md
  prompts/
    developer-luahelper-mcp.md   ← this file
```

Everything else is created according to the plan.

### Available Tools

- `lualsp.exe`: `C:\Users\dnmno\.vscode\extensions\yinfei.luahelper-0.2.29\server\lualsp.exe`
- Reference LSP client (Node.js): `C:\Users\dnmno\AppData\Local\Temp\luahelper_lsp.js` — working prototype showing how to communicate with lualsp.exe
- .NET 10 SDK installed
- VS Code with C# Dev Kit

### Report Format

After completing each phase, write a brief report:
- What was done
- Which tests passed
- DoD met / not met
- If not met — what remains

## Start

Begin with **Phase 0, Step 0.1** from `dev-plan-luahelper-mcp-server.md`.
