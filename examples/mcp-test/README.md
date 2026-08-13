# MCP test project

A small pseudo Lua project used to manually test the LuaHelper MCP server
after a release. It plants a known set of errors and warnings across the
files so you can verify diagnostics are reported correctly, and it ships a
custom `luahelper.json` so configuration merging can be exercised too.

## Layout

| File | Planted issues | Expected diagnostics (default checks) |
|---|---|---|
| `broken.lua` | syntax error — `if` without `then` | type 1 syntax errors (severity 1) |
| `config.lua` | duplicate table key, self-assignment, unused local | type 5 duplicate key, type 20 self-assign, type 4 unused local |
| `main.lua` | assignment param count mismatch, unused locals | type 8 param count, type 4 unused locals |
| `src/utils.lua` | unknown annotation type `Player`, unused locals | type 18 annotate type (severity 3), type 4 unused locals |
| `src/player.lua` | duplicate function params, duplicate table key | type 13 duplicate func param, type 5 duplicate key |
| `modules/inventory.lua` | duplicate table key (`name` in nested table) | type 5 duplicate key |
| `clean.lua` | — | no warnings (server times out → empty result) |

> Warning types: 1 = syntax error, 4 = unused local, 5 = duplicate key,
> 8 = param count, 13 = duplicate func param, 18 = annotate type,
> 20 = self-assignment. Severity: 1 = Error, 2 = Warning, 3 = Information.

Issues such as self-assignment and unused locals are reported only when the
corresponding check flag is enabled in the server's `DefaultChecks` (the
per-project `luahelper.json` only overrides `ShowWarnFlag`, `IgnoreModules`,
`IgnoreFileOrFloder`, `IgnoreFileErr`, and `PathSeparator`).

## How to run

1. Configure the MCP server in your client (see the repository root
   `README.md`).
2. Open `PROMPT.md`, replace `<PROJECT_ROOT>` with the absolute path to this
   directory, and paste the prompt into your assistant.
3. After the run, restore the custom config if `create_luahelper_json`
   overwrote it: `git checkout -- examples/mcp-test/luahelper.json`.

> Note: the server resolves the project configuration from the directory of
> the file being checked, so files in subdirectories (`src/`, `modules/`)
> pick up the config only when checking the whole project or a root-level
> file.