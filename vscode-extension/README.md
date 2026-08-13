# LuaHelper MCP Server

MCP server for Lua diagnostics powered by LuaHelper. Installs a Model Context
Protocol server that exposes Lua diagnostics tools to VS Code Copilot Chat.

## Features

- **Lua diagnostics in Copilot Chat** — ask Copilot to check a Lua file or a
  whole project; the assistant calls the MCP tools and reports warnings and
  errors with line numbers.
- **No extra setup** — the extension bundles the MCP server and the `lualsp`
  binary, so after installation the `luahelper` server starts automatically.
- **Zero runtime dependencies** — the server is a NativeAOT single-file binary,
  no .NET runtime required.

## Requirements

- VS Code 1.99 or newer
- GitHub Copilot Chat with Model Context Protocol support
- Windows (the bundled server binary is `LuaHelperMcpServer.exe`)

## How to use

After installation, VS Code starts the `luahelper` MCP server automatically.
Ask Copilot to check a Lua file, e.g.:

> Check examples/main.lua for Lua warnings

> Run diagnostics on the whole project at `C:\dev\my-lua-project`

The assistant calls the `check_lua_file` / `check_lua_project` tools and
reports the diagnostics with line numbers.

### Available tools

| Tool | Description | Parameters |
|---|---|---|
| `check_lua_file` | Run LuaHelper diagnostics on a single `.lua` file; returns warnings/errors with line numbers | `filePath` (string, required) |
| `check_lua_project` | Run diagnostics on an entire Lua project; scans all `.lua` files recursively | `projectPath` (string, required) |
| `get_supported_checks` | List all 22 LuaHelper check types with IDs and default enablement | — |
| `get_luahelper_version` | Report the version of the bundled `lualsp` binary | — |
| `get_server_version` | Report the version of this MCP server | — |
| `get_luahelper_config` | Show the effective configuration for a project (defaults merged with `luahelper.json`) | `projectPath` (string, required) |
| `create_luahelper_json` | Create a default `luahelper.json` in the project root | `projectPath` (string, required) |

The server also exposes resources (`luahelper://diagnostics/{+filePath}`,
`luahelper://config`) and prompts (`fix_lua_warnings`, `configure_luahelper`)
that guide the assistant when fixing diagnostics.

## Configuration

### `luahelper.json` (per project)

Place in your Lua project root to override the default check flags and ignore
specific globals or files:

| Field | Description |
|---|---|
| `ShowWarnFlag` | `1`/`0` master switch for all checks |
| `IgnoreModules` | Globals to ignore (e.g. globals your runtime injects) |
| `IgnoreFileOrFloder` | Files/folders to skip (defaults: `.vscode/`, `.git/`, `build/`, `vendor/`, `node_modules/`, and more) |
| `IgnoreFileErr` | Files/folders to skip in error reporting |
| `PathSeparator` | Separator for `require` paths (default `"."`) |

Use the `create_luahelper_json` tool in Copilot Chat to generate a starter
file with the recommended defaults.

### `lualsp` location

The server locates `lualsp` next to its own binary automatically. If it lives
elsewhere, set the `LUAHELPER_LUALSP_PATH` environment variable to its
absolute path.

## Troubleshooting

- **Copilot does not see the tools** — restart VS Code after installing the
  extension, and make sure Copilot Chat's MCP servers are enabled.
- **Check the log** — open the Output panel and select the "Copilot" channel
  to see MCP server startup messages.

## License

MIT — see [LICENSE](LICENSE).