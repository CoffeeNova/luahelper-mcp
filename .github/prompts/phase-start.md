# Prompt: Start Phase {N} — {Phase Name}

> Use this template to begin a new phase in a fresh session.

## Context

- **Phase:** {N} — {Phase Name}
- **Plan:** `.github/docs/dev-plan-luahelper-mcp-server.md` (section "Phase {N}")
- **Architecture:** `.github/docs/arch-luahelper-mcp-server.md` (relevant sections)
- **Previous phase:** {Phase N-1} completed — {brief summary}

## Tasks

{List the steps from the dev plan}

## Definition of Done

{List the DoD items from the dev plan}

## Workflow

1. Read the plan and architecture
2. Create a todo list
3. Implement step by step
4. Run `dotnet test src/LuaHelperMcpServer.Tests.Unit` after each step
5. Run `dotnet test src/LuaHelperMcpServer.Tests.Integration` before completion
6. Format with `csharpier format src`
7. Write a brief report
