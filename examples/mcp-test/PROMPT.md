# MCP test prompt

> Paste this prompt into your AI assistant (the MCP client) to run an
> end-to-end test of the LuaHelper MCP server against the sample project.
>
> **Before running:** replace every `<PROJECT_ROOT>` below with the absolute
> path to the test project, for example
> `E:\Repository\luahelper-mcp\examples\mcp-test` on Windows or
> `/path/to/luahelper-mcp/examples/mcp-test` on Linux/macOS.
>
> The prompt is self-contained: paste it as-is (after the replacement) and let
> the agent work through the steps.
>
> **Quick alternative:** instead of walking through every step manually, the
> agent may run the automated smoke test script
> `qa-test.ps1` (same directory) which exercises every API and prints a
> pass/fail summary. If used, the agent should still present the final report
> described in section 7.

---

You are a QA test harness for the **LuaHelper MCP server**. Our goal is to
verify that a freshly released version works correctly in a real environment,
so you must exercise **every API** exposed by the `luahelper` MCP server and
report the results. Do not skip steps, do not fix any of the files, and do not
stop on the first failure — record failures and continue.

Test project root: `<PROJECT_ROOT>` (always use absolute paths).

Run the steps below **in order** and report each result (success/failure +
the output you received).

## 1. Version and capability probes

- Call `get_server_version`. Confirm it returns the server version string.
- Call `get_luahelper_version`. Confirm it returns a `lualsp` version string.
- Call `get_supported_checks`. Confirm it returns the full list of check IDs,
  names, and default enablement (expect 21 checks).

## 2. Single-file diagnostics

Call `check_lua_file` on **each** file below and report the diagnostics
(verify the output includes line numbers, severities, and messages):

- `<PROJECT_ROOT>\broken.lua` — **must** report at least one syntax error.
- `<PROJECT_ROOT>\config.lua` — expect a duplicate table key warning.
- `<PROJECT_ROOT>\main.lua` — expect an assignment param count warning.
- `<PROJECT_ROOT>\src\utils.lua` — expect an annotation type warning
  (unknown type `Player`).
- `<PROJECT_ROOT>\src\player.lua` — expect duplicate function params and
  duplicate table key warnings.
- `<PROJECT_ROOT>\modules\inventory.lua` — expect a duplicate table key
  warning.
- `<PROJECT_ROOT>\clean.lua` — **must** report no warnings. Note: this file
  may take up to 10 seconds because lualsp sends no notification for clean
  files and the server times out before returning an empty result.

## 3. Project-wide diagnostics

- Call `check_lua_project` on `<PROJECT_ROOT>`. Confirm it scans all `.lua`
  files recursively and returns a per-file report.

## 4. Configuration API

- Call `get_luahelper_config` on `<PROJECT_ROOT>`. Confirm the returned
  configuration reflects the custom `luahelper.json` shipped with the project
  (`IgnoreModules`, `IgnoreFileOrFloder`, `PathSeparator`).
- Call `create_luahelper_json` on `<PROJECT_ROOT>`. Confirm it creates
  `luahelper.json` (this overwrites the shipped one — see cleanup below).
- Call `get_luahelper_config` on `<PROJECT_ROOT>` again. Confirm it now
  reflects the freshly generated default file.

## 5. Error handling

Verify the server returns a clear error (and does not hang or crash) for
invalid input:

- `check_lua_file` with a non-existent file path.
- `check_lua_project` with a non-existent directory.
- `get_luahelper_config` with a non-existent directory.

## 6. Resources and prompts (if your client supports them)

- If your client exposes MCP resources, try reading:
  - `luahelper://config` — returns the current config as JSON.
  - `luahelper://diagnostics/<PROJECT_ROOT>\clean.lua` — returns diagnostics
    for clean.lua (an empty JSON array is the expected result).
- If your client exposes MCP prompts, note that `fix_lua_warnings` and
  `configure_luahelper` are advertised.

## 7. Report

Produce a final report with:

1. Every tool called, its arguments, and whether it succeeded.
2. The diagnostics returned for each file (quote the output).
3. Any failures, unexpected output, empty results, hangs, or crashes.
4. A pass/fail summary per section (1-6) and an overall verdict.

## Cleanup

The sample project ships with a custom `luahelper.json`. After `create_luahelper_json` runs it is replaced by the generated default. Restore it before the next run:

```powershell
git checkout -- examples/mcp-test/luahelper.json
```