# Skill: Phase Workflow

Use at the start of every phase or feature implementation.

## Workflow

1. **Read the plan** — open `.github/docs/dev-plan-luahelper-mcp-server.md` and read the current phase
2. **Read the architecture** — open `.github/docs/arch-luahelper-mcp-server.md` for relevant sections
3. **Create a todo list** — break the phase into steps
4. **Implement step by step** — one step at a time, verify after each
5. **Run tests** — `dotnet test src/LuaHelperMcpServer.Tests.Unit` after each step
6. **Check DoD** — verify Definition of Done before moving to next phase
7. **Hand back** — write a brief report: what was done, tests passed, DoD met/not met

## Phase structure

Each phase in `dev-plan-luahelper-mcp-server.md` has:
- **Goal** — what the phase delivers
- **Estimated time** — rough time estimate
- **Steps** — numbered steps with tasks, commands, and DoD
- **DoD** — Definition of Done checklist

## Rules

- Don't skip ahead — each phase depends on the previous one
- Check DoD before moving on
- Run tests after each step
- Reference architecture doc for class designs, sequence diagrams, and state machine

## Report format

After completing each phase:

```markdown
## Phase N complete ✅

### What was done
- Step N.1: ...
- Step N.2: ...

### Test results
- Unit: X tests, 0 failures
- Integration: Y tests, 0 failures

### DoD met? ✅/❌
- [ ] Item 1
- [ ] Item 2
```
