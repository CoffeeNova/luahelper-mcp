# Skill: LSP Protocol — Content-Length Framed JSON-RPC

Use when writing, fixing, or debugging any LSP communication with `lualsp.exe`. Covers the wire format, message lifecycle, and lualsp.exe-specific quirks.

## Wire format

All messages use `Content-Length` framing (same as MCP):

```
Content-Length: {byteCount}\r\n\r\n{jsonBody}
```

- `Content-Length` is the **byte count** of the UTF-8 encoded JSON body (not character count)
- Headers are ASCII, body is UTF-8
- Header ends with `\r\n\r\n` (double CRLF)

## Message types

### Request (expects a response)
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{...}}
```

### Notification (no response expected)
```json
{"jsonrpc":"2.0","method":"initialized","params":{}}
```

### Response
```json
{"jsonrpc":"2.0","id":1,"result":{...}}
```

## LSP lifecycle

1. **initialize** (request) → server responds with capabilities
2. **initialized** (notification) — signals server is ready
3. **textDocument/didOpen** (notification) — send file content for analysis
4. Server responds with **textDocument/publishDiagnostics** (notification)
5. **shutdown** (request) → server responds
6. **exit** (notification) — terminates the server

## lualsp.exe specifics

### Initialize params
```json
{
  "processId": 1234,
  "rootUri": "file:///C:/project",
  "rootPath": "C:\\project",
  "capabilities": {
    "textDocument": {
      "synchronization": { "didOpen": true, "didChange": true }
    }
  },
  "initializationOptions": {
    "client": "vsc",
    "PluginPath": "C:\\path\\to\\luahelper-extension",
    "AllEnable": true,
    "CheckSyntax": true,
    "CheckAnnotateType": true,
    "IgnoreFileOrDir": [".vscode/", "one11.lua"],
    "IgnoreFileOrDirError": [".vscode/", "one11.lua"],
    "RequirePathSeparator": ".",
    "EnableReport": true
  }
}
```

### didOpen params
```json
{
  "textDocument": {
    "uri": "file:///C:/project/main.lua",
    "languageId": "lua",
    "version": 1,
    "text": "local x = 1"
  }
}
```

### publishDiagnostics response
```json
{
  "uri": "file:///C:/project/main.lua",
  "diagnostics": [
    {
      "range": {
        "start": {"line": 0, "character": 0},
        "end": {"line": 0, "character": 5}
      },
      "severity": 2,
      "warningType": 18,
      "message": "not define annotate type: Frame"
    }
  ]
}
```

## Key gotchas

- `PluginPath` must point to the **directory** containing lualsp.exe, not the exe itself
- `IgnoreFileOrDir` and `IgnoreFileOrDirError` must include `.vscode/` to avoid noise from VS Code workspace files
- `RequirePathSeparator` should be `"."` for Lua (dot notation for require paths)
- `EnableReport: true` enables diagnostic reporting
- The server sends `publishDiagnostics` as a **notification** (no `id`), not a response to `didOpen`
- Diagnostics may arrive asynchronously — use `TaskCompletionSource` to wait for them
- `severity` values: 1=Error, 2=Warning, 3=Information, 4=Hint
- `warningType` values are LuaHelper-specific (18 = annotate type warnings)

## Reference implementation

See `LspClient.cs` in `Services/` for the full C# implementation. Key methods:
- `EnsureInitializedAsync` — sends initialize + initialized
- `OpenFileAsync` — sends didOpen
- `ReadLoopAsync` — background reader dispatching responses and notifications
- `HandlePublishDiagnostics` — parses diagnostics into `LuaDiagnostic` models
