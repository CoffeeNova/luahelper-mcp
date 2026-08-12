# Prompt: Bugfix — {Bug Title}

> Use this template to report and fix a bug.

## Bug description

{What happens, what should happen}

## Reproduction steps

1. {Step 1}
2. {Step 2}
3. {Step 3}

## Environment

- .NET version: {version}
- lualsp.exe version: {version}
- OS: {OS}

## Diagnosis

{After investigation: root cause, affected files}

## Fix

{After implementation: what changed, which files}

## Verification

- [ ] Unit test added/updated: `dotnet test src/LuaHelperMcpServer.Tests.Unit`
- [ ] Integration test passes: `dotnet test src/LuaHelperMcpServer.Tests.Integration`
- [ ] Manual test: `dotnet run --project src/LuaHelperMcpServer -- "path/to/project"`
- [ ] Formatted with CSharpier
