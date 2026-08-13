# LuaHelper MCP Server — Agent Instructions

This file is the **entry point** for AI agents working in this repository.

Everything an agent needs — project context, architecture, skills, agents, prompts, and tools — lives in the **`.github/`** directory. It is the single source of truth.

## Reading order

1. `.github/CONTEXT.md` — what the project does, code conventions, key gotchas, environment setup
2. `.github/ARCHITECTURE.md` — module design, state machine, data flow, threading model, error handling
3. `.github/skills/lsp-protocol/SKILL.md` — **before writing/fixing any LSP communication**: Content-Length framing, JSON-RPC lifecycle, lualsp.exe quirks
4. `.github/skills/mcp-sdk-csharp/SKILL.md` — **before adding/modifying MCP tools**: attribute registration, DI wiring, stdio transport

## Rules

- Follow everything in `.github/`. It is the source of truth.
- When the design or plan changes, update the files in `.github/` **first** — they are the contract.
- `README.md` at the repo root is for **human users** — do not treat it as technical documentation.
- All documentation and code in this workspace must be written in English.
- **Contract-first**: update `.github/` before code — phase outcomes and deviations are recorded directly in `.github/docs/dev-plan-luahelper-mcp-server.md` (single source of truth; no separate notes directory).

## Skills (`.github/skills/`)

| Skill | When to use |
|---|---|
| `lsp-protocol` | Any LSP communication: Content-Length framing, JSON-RPC message structure, initialize/didOpen/publishDiagnostics/shutdown lifecycle, lualsp.exe-specific initializationOptions |
| `mcp-sdk-csharp` | Adding/modifying MCP tools: `[McpServerTool]` attributes, DI registration, stdio transport, tool parameter descriptions |
| `lualsp-exe` | Working with lualsp.exe: -mode=1 vs -mode=0, PluginPath, all 22 check flags, IgnoreFileOrDir, RequirePathSeparator |
| `dotnet-project` | Scaffolding .NET projects: solution files, console projects, test projects (NUnit), package references, InternalsVisibleTo |
| `nunit-testing` | Writing/running NUnit tests: Shouldly assertions, mocking with NSubstitute + AutoFixture, test project separation (Unit vs Integration) |
| `process-lifecycle` | Managing external processes in .NET: ProcessStartInfo, stdin/stdout redirect, crash detection, restart with exponential backoff |
| `csharpier-formatting` | Code formatting with CSharpier: installation, CLI usage, CI integration |
| `phase-workflow` | End-to-end phase/feature workflow (contract-first, todo, implement, verify, document, hand back) |

## Agents (`.github/agents/`)

- `luahelper-developer` — main agent for any phase/feature/bugfix (startup ritual + rules + output format).
- `dotnet-architect` — architecture review subagent: analyzes design decisions, validates against arch doc, suggests improvements.

## Prompts (`.github/prompts/`)

- `phase-start.md` — template to begin a phase in a fresh session (context + tasks + DoD + workflow).
- `bugfix.md` — structured bugfix workflow (reproduce → diagnose → fix → verify).

## Tools (`.github/tools/`)

- `build.ps1` — build all projects in the solution.
- `test.ps1` — run unit tests (fast) or all tests (with integration).
- `deploy.ps1` — publish the MCP server and copy lualsp.exe for distribution.
- `fetch-lualsp.ps1` — detect/download/update/bundle lualsp.exe (`lualsp/{rid}/` + `version.json`).
- `build-vsix.ps1` — build the VS Code extension: fetch lualsp → publish server → `vsce package`.

## Local development environment

- .NET 10 SDK required (`dotnet --version` → 10.x)
- `lualsp.exe` path configured via `LUAHELPER_LUALSP_PATH` env var, or falls back to `lualsp/win-x64/lualsp.exe`
- Integration tests use `LUAHELPER_EXTENSION_PATH` env var to locate lualsp.exe; skip gracefully if not set

## Unit tests (`src/LuaHelperMcpServer.Tests.Unit/`)

- Run: `dotnet test src/LuaHelperMcpServer.Tests.Unit`
- 20+ tests covering all non-UI services
- No filesystem access, no real processes — uses NSubstitute + AutoFixture + FakeLspServer
- **Before writing or running tests, read the `nunit-testing` skill**

## Integration tests (`src/LuaHelperMcpServer.Tests.Integration/`)

- Run: `dotnet test src/LuaHelperMcpServer.Tests.Integration`
- 8+ tests using real lualsp.exe
- Requires `LUAHELPER_EXTENSION_PATH` or falls back to default VS Code extension path
- Tests skip gracefully with `Assert.Ignore` if lualsp.exe not found
