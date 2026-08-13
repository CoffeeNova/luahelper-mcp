# LuaHelper MCP Server

A [Model Context Protocol](https://modelcontextprotocol.io) server that brings
[LuaHelper](https://github.com/Tencent/LuaHelper) diagnostics to AI coding
assistants. It wraps Tencent's `lualsp` LSP server for Lua and exposes its
checking capabilities as MCP tools, so GitHub Copilot, Claude Desktop, and
other MCP clients can ask for Lua warnings and errors in your project.

The server ships as a NativeAOT single-file binary (no .NET runtime required)
with the matching `lualsp` binary bundled per platform. It manages the LSP
process lifecycle for you: spawn, initialize, crash recovery with exponential
backoff, and shutdown.

## Quick start

### 1. Install

Download the release asset for your platform from the
[releases page](https://github.com/CoffeeNova/luahelper-mcp/releases):

| Platform | Asset |
|---|---|
| Windows | `luahelper-mcp-server-win-x64.zip` |
| Linux | `luahelper-mcp-server-linux-x64.zip` |
| macOS | `luahelper-mcp-server-osx-x64.zip` |

Extract the zip anywhere. It contains:

```
LuaHelperMcpServer[.exe]      the MCP server (NativeAOT, single file)
lualsp/{rid}/lualsp[.exe]     the bundled LuaHelper LSP server
lualsp/version.json           provenance manifest (version, sha256)
appsettings.json              server configuration
```

> Prefer VS Code? Install `luahelper-mcp-0.1.0.vsix` from the same release —
> the extension registers the MCP server with VS Code automatically.

### 2. Configure

Tell your MCP client where the server binary is.

**VS Code (Copilot Chat):**

```json
{
  "mcp.servers": {
    "luahelper": {
      "command": "C:\\path\\to\\LuaHelperMcpServer.exe",
      "args": []
    }
  }
}
```

**Claude Desktop** (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "luahelper": {
      "command": "/path/to/LuaHelperMcpServer",
      "args": []
    }
  }
}
```

The server locates `lualsp` next to its own binary automatically. If your
`lualsp` lives elsewhere, set the `LUAHELPER_LUALSP_PATH` environment variable
to its absolute path.

### 3. Use

Restart your client, then ask the assistant to check Lua files:

> Check `src/main.lua` for Lua warnings.
>
> Run diagnostics on the whole project at `E:\Games\MyAddon`.

The assistant calls the `check_lua_file` / `check_lua_project` tools and
reports the diagnostics with line numbers.

## Configuration reference

### `appsettings.json` (server)

Sits next to the server binary. All fields are optional.

| Key | Default | Description |
|---|---|---|
| `LuaHelper:LualspPath` | `""` | Path to the `lualsp` binary; empty = auto-detect `lualsp/{rid}/` next to the server (rid depends on the platform). Overridable with `LUAHELPER_LUALSP_PATH`. |
| `LuaHelper:DefaultTimeout` | `00:00:30` | Timeout for the LSP `initialize` handshake |
| `LuaHelper:DiagnosticTimeout` | `00:00:10` | How long to wait for diagnostics per file |
| `LuaHelper:MaxRestarts` | `3` | Max automatic restarts after a `lualsp` crash |
| `LuaHelper:BackoffScheduleSeconds` | `[2, 4, 8]` | Backoff delays between restart attempts |
| `LuaHelper:IdleTimeoutMinutes` | `10` | Reserved for future idle shutdown |
| `LuaHelper:DefaultChecks` | *(see below)* | Default check flags when no `luahelper.json` exists |

`DefaultChecks` holds all 22 LuaHelper check flags, e.g.
`CheckSyntax: true`, `CheckNoDefine: false`, `CheckAnnotateType: true`.
Run the `get_supported_checks` tool to list every flag with its default.

### `luahelper.json` (per project)

Place in your Lua project root to override server defaults. Fields map to
`lualsp`'s `initializationOptions`:

| Field | Maps to | Description |
|---|---|---|
| `ShowWarnFlag` | `AllEnable` | `1`/`0` master switch for all checks |
| `IgnoreModules` | `IgnoreModules` | Globals to ignore (e.g. WoW API: `C_Container`, `CreateFrame`, ...) |
| `IgnoreFileOrFloder` | `IgnoreFileOrDir` | Files/folders to skip (`".vscode/"`, `"Tests/"`) |
| `IgnoreFileErr` | `IgnoreFileOrDirError` | Files/folders to skip in error reporting |
| `PathSeparator` | `RequirePathSeparator` | Separator for `require` paths (default `"."`) |

Use the `create_luahelper_json` tool to generate a starter file with the
recommended WoW globals.

## Available MCP tools

| Tool | Description | Parameters |
|---|---|---|
| `check_lua_file` | Run LuaHelper diagnostics on a single `.lua` file; returns warnings/errors with line numbers | `filePath` (string, required) |
| `check_lua_project` | Run diagnostics on an entire Lua project; scans all `.lua` files recursively | `projectPath` (string, required) |
| `get_supported_checks` | List all 22 LuaHelper check types with IDs and default enablement | — |
| `get_luahelper_version` | Report the version of the bundled `lualsp` binary | — |
| `get_server_version` | Report the version of this MCP server (e.g. `0.1.0`) | — |
| `get_luahelper_config` | Show the effective configuration for a project (defaults merged with `luahelper.json`) | `projectPath` (string, required) |
| `create_luahelper_json` | Create a default `luahelper.json` in the project root | `projectPath` (string, required) |

Resources: `luahelper://diagnostics/{+filePath}` (JSON diagnostics for a file)
and `luahelper://config` (current configuration).

Prompts: `fix_lua_warnings` and `configure_luahelper` guide assistants toward
fixing diagnostics and tuning configuration.

## Development

### Requirements

- .NET 10 SDK (`dotnet --version` → 10.x)
- NativeAOT publishing needs the platform linker (Visual Studio C++ workload
  on Windows, clang on Linux/macOS) — CI runners have it preinstalled

### Build and test

```powershell
# Build all projects
dotnet build

# Unit tests (fast, no external dependencies)
dotnet test src/LuaHelperMcpServer.Tests.Unit

# Integration tests (provision lualsp first, then run)
.github/tools/fetch-lualsp.ps1 -Rid win-x64 -Update
dotnet test src/LuaHelperMcpServer.Tests.Integration

# Format code
csharpier format src
```

Tests use NUnit with **Shouldly** assertions and **NSubstitute + AutoFixture**
mocking (see `.github/skills/nunit-testing/SKILL.md`).

### Tooling

- `.github/tools/fetch-lualsp.ps1` — detect/download/update/bundle `lualsp`
  per platform (idempotent; `-Update` for CI)
- `.github/tools/build-vsix.ps1` — publish the server and package the VS Code
  extension (`.vsix`)
- `.github/tools/deploy.ps1` — publish a standalone AOT binary + bundled
  `lualsp` into `publish/{rid}/`
- `.github/tools/smoke-test-mcp.ps1` — full MCP handshake against a published
  binary (used by CI)
- `.github/workflows/ci.yml` — build, unit + integration tests, NativeAOT
  publish with handshake verification on every push
- `.github/workflows/release.yml` — semver release pipeline (see
  [Versioning](#versioning))

### Versioning

Releases are fully automated:

- **Every push to `main`** triggers the release pipeline, which bumps the
  **patch** version only (latest tag `v0.1.5` → `v0.1.6`). First release is
  `v0.1.0`.
- **Bumping minor/major**: create and push a tag yourself, e.g.
  `git tag v1.0.0 && git push origin v1.0.0`. The tag version is used as-is.
- The crafted version is stamped into every artifact: the server assembly
  (ask the assistant to call `get_server_version`, or run
  `LuaHelperMcpServer.exe` — it reports the version via the tool), the
  `.vsix` manifest, zip names, and the GitHub release. Asset names are
  `luahelper-mcp-server-{rid}-{version}.zip` and `luahelper-mcp-{version}.vsix`.

### Contributing

This repository follows a contract-first workflow: design decisions live in
`.github/docs/` (architecture + dev plan) and are the source of truth for
implementation. Open an issue or PR for changes; CI verifies builds, tests,
and the AOT binary.

## License

MIT (this repository's code) — see [LICENSE](LICENSE).

The bundled `lualsp` binaries are from the
[LuaHelper project](https://github.com/Tencent/LuaHelper) (Copyright (c)
Tencent), distributed under the BSD-3-Clause license — see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).