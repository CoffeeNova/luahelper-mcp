# Skill: MCP C# SDK — Tool Registration & Hosting

Use when adding, modifying, or debugging MCP tools in this project. Covers the `ModelContextProtocol` NuGet package, attribute-based tool registration, DI wiring, and stdio transport.

## NuGet packages

| Package | Purpose |
|---|---|
| `ModelContextProtocol` | Main package — hosting + DI extensions. **Use this one.** |
| `ModelContextProtocol.Core` | Minimal — client + low-level server APIs |
| `ModelContextProtocol.AspNetCore` | HTTP-based MCP servers (not used here) |

## Tool registration pattern

### 1. Define a tool class

```csharp
[McpServerToolType]
public sealed class LuaDiagnosticTools
{
    private readonly ILspClient _lspClient;

    public LuaDiagnosticTools(ILspClient lspClient)
    {
        _lspClient = lspClient;
    }

    [McpServerTool(Name = "check_lua_file")]
    [Description("Run LuaHelper diagnostics on a single .lua file.")]
    public async Task<string> CheckLuaFile(
        [Description("Absolute path to the .lua file to check")] string filePath,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

### 2. Register in DI

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();  // Scans for [McpServerToolType] classes
```

### 3. Register service dependencies

```csharp
builder.Services.AddSingleton<ILspClient, LspClient>();
builder.Services.AddSingleton<IDiagnosticCache, DiagnosticCache>();
```

## Critical rules

- **Use `Host.CreateEmptyApplicationBuilder`** (not `CreateApplicationBuilder`) to avoid console output that corrupts stdio JSON-RPC
- **All logging goes to stderr** — configure with `LogToStandardErrorThreshold = LogLevel.Trace`
- Tool methods return `Task<string>` — the string is the tool result content
- Handle errors gracefully — return error strings with `isError: true` rather than throwing
- Parameters must be decorated with `[Description]` — this becomes the JSON Schema description visible to the AI

## Tool result format

```csharp
// Success
return $"Found 3 warnings in {filePath}:\nL18: ...";

// Error
return $"Error: File not found: {filePath}";
```

The MCP SDK wraps this in the proper JSON-RPC response automatically.

## Key gotchas

- `WithToolsFromAssembly()` scans the calling assembly — make sure tool classes are in the same assembly as `Program.cs`
- Tool classes must have `[McpServerToolType]` attribute (class level)
- Tool methods must have `[McpServerTool]` attribute (method level)
- Tool methods can accept `CancellationToken` as the last parameter — the SDK passes it automatically
- Do NOT use `Console.WriteLine` in tool methods — it corrupts the stdio JSON-RPC stream
- Use `ILogger<T>` for all diagnostic output (goes to stderr)
- **MCP stdio framing is newline-delimited JSON-RPC** (one JSON message per line, LF or CRLF) — NOT `Content-Length` framing like LSP. The SDK handles framing internally; never write frames yourself.
- To smoke-test a running server: pipe `{"jsonrpc":"2.0","id":1,"method":"initialize",...}\n` then `tools/list` as newline-delimited lines into the process and read newline-delimited responses. Do NOT hand-craft `Content-Length:` frames.

## Reference

- `Program.cs` in the project root for DI setup
- `Tools/` directory for tool implementations (Phase 1+)
