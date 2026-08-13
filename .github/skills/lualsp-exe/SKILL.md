# Skill: lualsp.exe — LuaHelper LSP Server

Use when working with the `lualsp.exe` binary. Covers command-line flags, initialization options, check flags, and known behaviors.

## Binary location

- Installed by the LuaHelper VS Code extension: `{extensionPath}/server/lualsp.exe`
- Also bundled in this repo at `lualsp/{rid}/lualsp.exe` (gitignored)
- ~10 MB Go binary, single-file, no dependencies
- License: BSD-3-Clause (allows redistribution with attribution)

## Command-line flags

| Flag | Purpose |
|---|---|
| `-mode=1` | **LSP mode** — communicates via JSON-RPC over stdin/stdout. **Always use this.** |
| `-mode=0` | Command-line mode — processes a single file and exits. Produces no output in testing. **Do not use.** |
| `-logflag=0` | Suppresses internal logging. Use `-logflag=1` for debugging. |

## Initialization options (all 22 check flags)

These are passed in the `initializationOptions` field of the LSP `initialize` request:

| Flag | Default | Description |
|---|---|---|
| `AllEnable` | `true` | Master switch for all checks |
| `CheckSyntax` | `true` | Lua syntax errors |
| `CheckNoDefine` | `false` | Variable used but not defined |
| `CheckAfterDefine` | `false` | Variable used before definition |
| `CheckLocalNoUse` | `false` | Local variable declared but never used |
| `CheckTableDuplicateKey` | `true` | Duplicate keys in table literals |
| `CheckReferNoFile` | `false` | Reference to file that doesn't exist |
| `CheckAssignParamNum` | `true` | Function call argument count mismatch |
| `CheckLocalDefineParamNum` | `true` | Local function parameter count mismatch |
| `CheckGotoLable` | `true` | Invalid goto labels |
| `CheckFuncParam` | `false` | Function parameter issues |
| `CheckImportModuleVar` | `false` | Import module variable issues |
| `CheckIfNotVar` | `false` | If condition is not a variable |
| `CheckFunctionDuplicateParam` | `true` | Duplicate function parameters |
| `CheckBinaryExpressionDuplicate` | `false` | Duplicate binary expressions |
| `CheckErrorOrAlwaysTrue` | `false` | Error or always-true conditions |
| `CheckErrorAndAlwaysFalse` | `false` | Error and always-false conditions |
| `CheckNoUseAssign` | `false` | Assignment that is never used |
| `CheckAnnotateType` | `true` | EmmyLua annotation type checking |
| `CheckDuplicateIf` | `true` | Duplicate if conditions |
| `CheckSelfAssign` | `false` | Self-assignment (x = x) |
| `CheckFloatEq` | `false` | Float equality comparison |

## Additional initialization options

| Field | Type | Description |
|---|---|---|
| `PluginPath` | string | Directory containing lualsp.exe (NOT the exe path) |
| `FileAssociationsConfig` | object | File association overrides (usually empty `{}`) |
| `IgnoreFileOrDir` | string[] | Files/directories to skip for all checks |
| `IgnoreFileOrDirError` | string[] | Files/directories to skip for error checks |
| `RequirePathSeparator` | string | Separator for require paths (`"."` for Lua) |
| `EnableReport` | bool | Enable diagnostic reporting |

## Known behaviors

- lualsp.exe analyzes files **when they are opened** via `textDocument/didOpen`
- Diagnostics are pushed asynchronously via `textDocument/publishDiagnostics`
- The server maintains an in-memory AST and symbol table across all opened files
- Opening a file that's already open updates its content (version tracking)
- The server does NOT support `textDocument/didChange` — close and re-open to update
- `IgnoreFileOrDir` patterns are matched against the file path (substring match)
- `.vscode/` and `one11.lua` are the default ignored paths

## Warning types

| Type ID | Description |
|---|---|
| 1 | Syntax error |
| 2 | Undefined variable |
| 3 | Variable used before definition |
| 4 | Unused local variable |
| 5 | Duplicate table key |
| 6 | Missing referenced file |
| 7 | Argument count mismatch |
| 8 | Parameter count mismatch |
| 9 | Invalid goto |
| 10 | Function parameter issue |
| 11 | Import module issue |
| 12 | If-not-var issue |
| 13 | Duplicate function parameter |
| 14 | Duplicate binary expression |
| 15 | Always-true condition |
| 16 | Always-false condition |
| 17 | Unused assignment |
| 18 | **Annotate type warning** (most common in WoW addons) |
| 19 | Duplicate if condition |
| 20 | Self-assignment |
| 21 | Float equality |
