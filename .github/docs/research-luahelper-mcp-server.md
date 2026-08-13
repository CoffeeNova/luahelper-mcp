# Research: LuaHelper MCP Server for Lua Diagnostics

> **Date:** 2026-08-12
> **Author:** AI Research Agent
> **Purpose:** Comprehensive research to decide whether building an MCP server that wraps `lualsp.exe` (LuaHelper) is viable, and to provide architecture/planning input for a follow-up agent.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [What is MCP?](#2-what-is-mcp)
3. [MCP SDK Landscape](#3-mcp-sdk-landscape)
4. [LuaHelper lualsp.exe — How It Works](#4-luahelper-lualsp-exe--how-it-works)
5. [Integration Strategy: lualsp.exe ↔ MCP](#5-integration-strategy-lualspexe--mcp)
6. [VS Code Extension Packaging](#6-vs-code-extension-packaging)
7. [Recommendation: .NET (C#)](#7-recommendation-net-c)
8. [Risks and Mitigations](#8-risks-and-mitigations)
9. [Open Questions for the Architect](#9-open-questions-for-the-architect)
10. [Appendix: Verified lualsp.exe Diagnostics Output](#10-appendix-verified-lualspexe-diagnostics-output)

---

## 1. Executive Summary

**Idea:** Build an MCP (Model Context Protocol) server that wraps LuaHelper's `lualsp.exe` and exposes Lua diagnostics (warnings, errors) as MCP tools/resources. An AI coding assistant (like GitHub Copilot) could then query this server to get real-time Lua linting results for any project.

**Verdict: HIGHLY VIABLE.** The MCP ecosystem is mature, the C# SDK is production-ready (v2.1.0, maintained by Microsoft), and `lualsp.exe` already speaks JSON-RPC over stdio — the same transport MCP uses. The integration is a thin translation layer: LSP `textDocument/publishDiagnostics` → MCP tools/resources.

**Key numbers:**
- `lualsp.exe` is a **10 MB** Go binary, single-file, no dependencies
- LSP protocol is JSON-RPC 2.0 with `Content-Length` headers — same wire format as MCP
- The existing LSP client script (`luahelper_lsp.js`) proves the integration works end-to-end
- VS Code Marketplace publishing is well-documented via `vsce`

---

## 2. What is MCP?

MCP (Model Context Protocol) is an open standard (created by Anthropic, now governed by a community) for connecting AI applications to external systems. It is analogous to a "USB-C port for AI."

### Core Concepts

| Concept | Description |
|---|---|
| **Tools** | Functions the LLM can call (with user approval). Have a name, description, and JSON Schema input. |
| **Resources** | File-like data the LLM can read (e.g., API responses, file contents). |
| **Prompts** | Pre-written templates for specific tasks. |

### Transport

| Transport | Use Case |
|---|---|
| **stdio** | Local servers — the MCP server runs as a child process, communicates over stdin/stdout. **This is what we need.** |
| **Streamable HTTP** | Remote servers — the MCP server runs as an HTTP endpoint. |

### Protocol

- JSON-RPC 2.0 messages with `Content-Length: N\r\n\r\n` headers
- Same wire format as LSP (Language Server Protocol)!
- Lifecycle: `initialize` → `initialized` → tools/resources/prompts calls → `shutdown` → `exit`

### Key Specification Pages

- [Architecture](https://modelcontextprotocol.io/specification/2026-07-28/architecture/index.md)
- [Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools.md)
- [Resources](https://modelcontextprotocol.io/specification/2026-07-28/server/resources.md)
- [stdio Transport](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/stdio.md)

---

## 3. MCP SDK Landscape

### Official SDKs (all maintained by the MCP organization)

| Language | Package | Status | Notes |
|---|---|---|---|
| **C# (.NET)** | `ModelContextProtocol` (NuGet) | **Tier 1** — v2.1.0, maintained by Microsoft | `[McpServerTool]` attributes, DI, hosting. **Best for a .NET senior dev.** |
| TypeScript | `@modelcontextprotocol/server` (npm) | Tier 1 | `McpServer` class, Zod schemas |
| Python | `mcp` (PyPI) | Tier 1 | `MCPServer` class, decorators |
| Java | Spring AI Boot Starter | Tier 1 | Spring Boot auto-config |
| Go | `github.com/modelcontextprotocol/go-sdk` | Tier 2 | Manual tool registration |
| Kotlin | `io.modelcontextprotocol:kotlin-sdk` | Tier 2 | Coroutine-based |
| Ruby | `mcp` gem | Tier 2 | |
| Rust | `rmcp` crate | Tier 2 | |

### C# SDK NuGet Packages

| Package | Description |
|---|---|
| `ModelContextProtocol.Core` | Minimal — client + low-level server APIs, minimum dependencies |
| **`ModelContextProtocol`** | **Main package** — hosting + DI extensions. **Recommended for our server.** |
| `ModelContextProtocol.AspNetCore` | HTTP-based MCP servers |
| `ModelContextProtocol.Extensions.Apps` | Interactive UI apps inside MCP hosts |
| `ModelContextProtocol.Extensions.Tasks` | Long-running async tool invocations |

### C# SDK Quickstart Pattern

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
```

Tools are defined as static classes with `[McpServerToolType]` and `[McpServerTool]` attributes:

```csharp
[McpServerToolType]
public static class LuaDiagnosticTools
{
    [McpServerTool, Description("Run Lua syntax check on a file")]
    public static async Task<string> CheckLuaSyntax(
        [Description("Absolute path to the .lua file")] string filePath)
    {
        // ... implementation
    }
}
```

---

## 4. LuaHelper lualsp.exe — How It Works

### Binary

- **Path:** `C:\Users\dnmno\.vscode\extensions\yinfei.luahelper-0.2.29\server\lualsp.exe`
- **Size:** ~10 MB (10,281,984 bytes)
- **Language:** Go (compiled to a single native executable)
- **Dependencies:** None — it's a standalone binary

### CLI Interface

```
Usage of lualsp.exe:
  -localpath string   local project path
  -logflag int        0 is not open log, 1 is open log
  -mode int           mode type, 0 is run cmd, 1 is local rpc, 2 is socket rpc
```

| Mode | Description |
|---|---|
| `0` (cmd) | One-shot analysis. Output goes to... (appears to be stdout but we got empty output — may need investigation) |
| **`1` (local rpc)** | **LSP mode** — listens on stdin/stdout, speaks LSP protocol. **This is what we use.** |
| `2` (socket rpc) | Listens on a TCP socket (port 7778) |

### LSP Protocol (mode 1)

The server speaks the Language Server Protocol (LSP), which is JSON-RPC 2.0 with `Content-Length` headers — **the same wire format as MCP**.

**Initialization options** (passed in `initialize` → `initializationOptions`):

```json
{
  "client": "vsc",
  "PluginPath": "<extension-path>",
  "FileAssociationsConfig": {},
  "AllEnable": true,
  "CheckSyntax": true,
  "CheckNoDefine": false,
  "CheckAfterDefine": false,
  "CheckLocalNoUse": false,
  "CheckTableDuplicateKey": true,
  "CheckReferNoFile": false,
  "CheckAssignParamNum": true,
  "CheckLocalDefineParamNum": true,
  "CheckGotoLable": true,
  "CheckFuncParam": false,
  "CheckImportModuleVar": false,
  "CheckIfNotVar": false,
  "CheckFunctionDuplicateParam": true,
  "CheckBinaryExpressionDuplicate": false,
  "CheckErrorOrAlwaysTrue": false,
  "CheckErrorAndAlwaysFalse": false,
  "CheckNoUseAssign": false,
  "CheckAnnotateType": true,
  "CheckDuplicateIf": true,
  "CheckSelfAssign": false,
  "CheckFloatEq": false,
  "CheckClassField": false,
  "CheckConstAssign": false,
  "CheckFuncParamType": false,
  "CheckFuncReturnType": false,
  "IgnoreFileOrDir": [".vscode/", "one11.lua"],
  "IgnoreFileOrDirError": [".vscode/", "one11.lua"],
  "RequirePathSeparator": ".",
  "EnableReport": true
}
```

**Diagnostics are delivered via** `textDocument/publishDiagnostics` notifications (LSP standard).

### Warning Types (from LuaHelper docs)

| Type | Description | Default |
|---|---|---|
| 1 | Syntax errors | ON |
| 2 | Variable not defined | OFF |
| 3 | Global used before defined | OFF |
| 4 | Local defined but not used | OFF |
| 5 | Duplicate table keys | ON |
| 6 | Referenced file not found | OFF |
| 7 | Assignment param count mismatch | ON |
| 8 | Local definition param count mismatch | ON |
| 9 | Goto label not found | ON |
| 10 | Function call param count > definition | OFF |
| 11 | Import module var not defined | OFF |
| 12 | If-not block error | OFF |
| 13 | Duplicate function params | ON |
| 14 | Duplicate binary expression | OFF |
| 15 | OR always true | OFF |
| 16 | AND always false | OFF |
| 17 | Unused assignment | OFF |
| **18** | **Annotation type warnings** | **ON** |
| 19 | Duplicate if conditions | ON |
| 20 | Self-assignment | OFF |
| 21 | Float equality | OFF |

### Configuration File (`luahelper.json`)

LuaHelper supports a project-level config file placed at the workspace root. It can:
- Set `BaseDir` for file resolution
- Ignore specific files/folders
- Ignore specific warning types per file or globally
- Ignore specific undefined variables
- Set `IgnoreModules` for framework-specific globals

**This is important:** We can ship a default `luahelper.json` with the MCP server that pre-configures WoW API globals as ignored modules.

---

## 5. Integration Strategy: lualsp.exe ↔ MCP

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    AI Assistant (Copilot)                │
│                      (MCP Client)                       │
└──────────────────────┬──────────────────────────────────┘
                       │ JSON-RPC (stdio)
                       ▼
┌─────────────────────────────────────────────────────────┐
│              LuaHelper MCP Server (.NET)                 │
│                                                         │
│  ┌─────────────┐   ┌────────────────────────────────┐   │
│  │ MCP Layer   │──▶│ Tool: check_lua_file(path)     │   │
│  │ (stdio)     │   │ Tool: check_lua_project(path)  │   │
│  │             │   │ Tool: get_luahelper_config()   │   │
│  │             │   │ Resource: luahelper://warnings │   │
│  └─────────────┘   └───────────┬────────────────────┘   │
│                                │                         │
│                                ▼                         │
│  ┌────────────────────────────────────────────────┐      │
│  │         lualsp.exe (child process)              │      │
│  │         LSP mode (-mode=1)                      │      │
│  │         JSON-RPC over stdin/stdout              │      │
│  └────────────────────────────────────────────────┘      │
└─────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Startup:** MCP server spawns `lualsp.exe -mode=1` as a child process
2. **Initialize:** MCP server sends LSP `initialize` with the project path and check flags
3. **Open files:** For each file to check, MCP server sends `textDocument/didOpen`
4. **Collect diagnostics:** MCP server receives `textDocument/publishDiagnostics` notifications
5. **Expose via MCP:** Diagnostics are returned as tool results or exposed as resources
6. **Shutdown:** On MCP shutdown, send LSP `shutdown` + `exit`, kill child process

### Proposed MCP Tools

| Tool | Description | Input |
|---|---|---|
| `check_lua_file` | Run diagnostics on a single Lua file | `filePath: string` |
| `check_lua_project` | Run diagnostics on an entire project | `projectPath: string`, `config?: LuaHelperConfig` |
| `get_luahelper_version` | Get the lualsp.exe version | (none) |
| `get_supported_checks` | List all available check types | (none) |

### Proposed MCP Resources

| Resource | Description |
|---|---|
| `luahelper://diagnostics/{filePath}` | Current diagnostics for a specific file |
| `luahelper://config` | Current LuaHelper configuration |

### Proposed MCP Prompts

| Prompt | Description |
|---|---|
| `fix_lua_warnings` | Template: "Analyze this Lua file and suggest fixes for all warnings" |
| `configure_luahelper` | Template: "Help me configure luahelper.json for my project" |

### Lifecycle Management

- **lualsp.exe is stateful** — it maintains an in-memory representation of the project
- On `check_lua_project`, send `textDocument/didOpen` for all `.lua` files, wait for diagnostics, then keep the process alive for subsequent requests
- On `check_lua_file`, send `textDocument/didOpen` for just that file
- **Restart strategy:** If `lualsp.exe` crashes, the MCP server should respawn it and re-initialize
- **Idle timeout:** Kill the child process after N minutes of inactivity to save resources

---

## 6. VS Code Extension Packaging

### Option A: Pure MCP Server (no VS Code extension)

The MCP server is a standalone .NET executable. Users configure it in their MCP client (VS Code, Claude Desktop, etc.) via:

```json
{
  "mcpServers": {
    "luahelper": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\LuaHelperMcpServer"]
    }
  }
}
```

**Pros:** Simple, no Marketplace publishing needed initially.
**Cons:** Requires .NET SDK installed, manual path configuration.

### Option B: VS Code Extension that bundles the MCP server

The extension:
1. Bundles the compiled .NET MCP server as a platform-specific binary (or requires .NET runtime)
2. Bundles `lualsp.exe` (or downloads it on first use)
3. Registers itself as an MCP server provider for VS Code
4. Can be published to the VS Code Marketplace

**Pros:** One-click install, auto-configuration, discoverable via Marketplace.
**Cons:** More complex build pipeline, need to handle platform-specific binaries.

### Publishing to VS Code Marketplace

1. **Create a publisher** at `https://marketplace.visualstudio.com/manage`
2. **Install `vsce`:** `npm install -g @vscode/vsce`
3. **Package:** `vsce package` → produces `.vsix`
4. **Publish:** `vsce publish` (requires PAT or Entra ID auth)

**Note:** As of December 2026, global PATs are retired. Use Entra ID workload identity federation for automated publishing.

### Extension Manifest (`package.json`)

```json
{
  "name": "luahelper-mcp",
  "displayName": "LuaHelper MCP Server",
  "description": "MCP server for Lua diagnostics powered by LuaHelper",
  "version": "0.1.0",
  "publisher": "your-publisher-id",
  "engines": { "vscode": "^1.90.0" },
  "categories": ["Linters", "Programming Languages"],
  "activationEvents": [],
  "main": "./out/extension.js",
  "contributes": {
    "mcpServerDefinitionProviders": [
      {
        "id": "luahelper",
        "label": "LuaHelper MCP Server"
      }
    ]
  }
}
```

**Important:** VS Code registers extension MCP servers via `contributes.mcpServerDefinitionProviders` (in `package.json`) plus the `vscode.lm.registerMcpServerDefinitionProvider` API (stable since VS Code 1.101). There is **no** `contributes.mcpServers` contribution point — that key is silently ignored (confirmed Aug 2026 against the official [MCP developer guide](https://code.visualstudio.com/api/extension-guides/ai/mcp)).

---

## 7. Recommendation: .NET (C#)

### Why .NET is the best choice

| Factor | .NET (C#) | TypeScript/Node | Python | Go |
|---|---|---|---|---|
| **Your expertise** | ★★★★★ Senior dev | ★★★ | ★★ | ★ |
| **SDK maturity** | ★★★★★ v2.1.0, Microsoft-maintained | ★★★★★ | ★★★★★ | ★★★ |
| **AOT compilation** | ✅ NativeAOT → single .exe | ❌ Requires Node | ❌ Requires Python | ✅ |
| **Process management** | ★★★★★ `System.Diagnostics.Process` | ★★★ | ★★★ | ★★★★ |
| **JSON-RPC parsing** | ★★★★★ `System.Text.Json` | ★★★★★ | ★★★★ | ★★★★ |
| **DI/Hosting** | ★★★★★ Built-in | ★★★ (manual) | ★★ | ★ |
| **Cross-platform** | ✅ .NET 10+ runs everywhere | ✅ | ✅ | ✅ |

### Key .NET Packages

```xml
<PackageReference Include="ModelContextProtocol" Version="2.1.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

### NativeAOT for Single-File Distribution

.NET 10+ supports NativeAOT compilation, producing a single native executable (~5-15 MB) with no runtime dependencies. This is ideal for an MCP server that needs to be easily distributed.

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true
```

### Project Structure (suggested)

```
LuaHelperMcpServer/
├── src/
│   ├── LuaHelperMcpServer/          # Main project
│   │   ├── Program.cs               # Entry point, DI setup
│   │   ├── Tools/
│   │   │   ├── LuaDiagnosticTools.cs    # MCP tool definitions
│   │   │   └── LuaHelperConfigTools.cs  # Config management tools
│   │   ├── Services/
│   │   │   ├── LuaHelperProcess.cs      # lualsp.exe lifecycle manager
│   │   │   ├── LspClient.cs             # LSP protocol client
│   │   │   └── DiagnosticCache.cs       # Cache for diagnostics results
│   │   ├── Models/
│   │   │   ├── LuaDiagnostic.cs         # Diagnostic model
│   │   │   └── LuaHelperConfig.cs       # Configuration model
│   │   └── appsettings.json
│   └── LuaHelperMcpServer.Tests/    # Unit tests
├── lualsp/                          # Bundled lualsp.exe (platform-specific)
│   ├── win-x64/lualsp.exe
│   ├── linux-x64/lualsp
│   └── osx-x64/lualsp
├── vscode-extension/                # VS Code extension wrapper (optional)
│   ├── package.json
│   ├── extension.js
│   └── .vscodeignore
├── LuaHelperMcpServer.sln
└── README.md
```

---

## 8. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **lualsp.exe is a Go binary** — we can't modify it | Medium | It's stable and well-tested. We wrap it, don't modify it. |
| **LSP protocol differences** — LSP and MCP both use JSON-RPC but have different message schemas | Low | We already have a working LSP client (`luahelper_lsp.js`). The translation is straightforward. |
| **lualsp.exe crashes** | Medium | Implement auto-restart with exponential backoff. Cache last-known-good diagnostics. |
| **Large project scan time** | Medium | Stream diagnostics incrementally. Use `textDocument/didOpen` per-file rather than full project scan. |
| **lualsp.exe licensing** | Low | LuaHelper is BSD-3-Clause licensed. Redistribution is allowed with attribution. |
| **.NET NativeAOT limitations** | Low | Some reflection-based features don't work with AOT. The MCP SDK uses attributes which are AOT-compatible. |
| **VS Code MCP contribution point** — may not be stable API | Medium | Fall back to manual `mcpServers` configuration in `settings.json`. |

---

## 9. Open Questions for the Architect

1. **Should we bundle lualsp.exe or download it?** Bundling is simpler for users but increases package size (~10 MB per platform). Downloading requires network access on first use.

2. **Should we support all 21 warning types or start with a subset?** The ArenaChillPrep use case only needs type 18 (annotation warnings). But a general-purpose tool should support all types.

3. **Should we support `luahelper.json` project config?** This would allow users to customize checks per project (e.g., ignore WoW globals).

4. **Should we build the VS Code extension wrapper from day 1, or start as a standalone MCP server?** Starting standalone is faster to prototype; the extension can be added later.

5. **What about the `-mode=0` (cmd) mode?** It produced no output in testing. If it can be made to work, it would be simpler than the LSP mode (no state management). Needs investigation.

6. **Should we support Streamable HTTP transport in addition to stdio?** This would allow remote usage (e.g., CI pipelines). The `ModelContextProtocol.AspNetCore` package supports this.

7. **How should we handle the `PluginPath` initialization option?** The VS Code extension passes its own path. For a standalone server, we need to know where `lualsp.exe` lives.

---

## 10. Appendix: Verified lualsp.exe Diagnostics Output

### Test Run (2026-08-12)

**Project:** `E:\Repository\ArenaChillPrep` (15 Lua files)
**Mode:** LSP mode (`-mode=1`)
**Checks enabled:** Default LuaHelper settings (AllEnable=true, CheckAnnotateType=true, etc.)

**Result: 17 diagnostics across 6 files, all `[Warn type:18]` (annotation type warnings)**

| File | Line | Message |
|---|---|---|
| `Classes/TradeManager.lua` | 45 | annotate warn: not find annotate type |
| `Classes/OptionsUI.lua` | 18 | not define annotate type: Frame |
| `Classes/OptionsUI.lua` | 21 | not define annotate type: CheckButton |
| `Classes/OptionsUI.lua` | 30 | annotate warn: not find annotate type |
| `Classes/OptionsUI.lua` | 138 | not define annotate type: Frame |
| `Classes/OptionsUI.lua` | 143 | not define annotate type: Frame |
| `Classes/OptionsUI.lua` | 160 | not define annotate type: FontString |
| `Classes/OptionsUI.lua` | 171 | not define annotate type: CheckButton |
| `Classes/OptionsUI.lua` | 195 | not define annotate type: Slider |
| `Classes/OptionsUI.lua` | 229 | not define annotate type: Frame |
| `Classes/OptionsUI.lua` | 288 | not define annotate type: Frame |
| `Data/Items.lua` | 18 | duplicate annotate type: Items |
| `Utils/Timers.lua` | 23 | annotate warn: not find annotate type |
| `Utils/Items.lua` | 8 | duplicate annotate type: Items |
| `Classes/Events.lua` | 17 | not define annotate type: Frame |
| `Classes/Events.lua` | 20 | annotate warn: not find annotate type |
| `Classes/Events.lua` | 31 | not define annotate type: Frame |

### LSP Client Script (for reference)

The working LSP client is at `C:\Users\dnmno\AppData\Local\Temp\luahelper_lsp.js`. It:
1. Spawns `lualsp.exe -mode=1 -logflag=0`
2. Sends LSP `initialize` with all check flags
3. Waits for `textDocument/publishDiagnostics` notifications
4. Prints all diagnostics grouped by file

This script can serve as a reference implementation for the LSP client component of the MCP server.

---

## References

- [MCP Specification](https://modelcontextprotocol.io/specification/2026-07-28/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) — v2.1.0, Apache 2.0
- [MCP C# SDK Docs](https://csharp.sdk.modelcontextprotocol.io/)
- [LuaHelper (Tencent)](https://github.com/Tencent/LuaHelper) — BSD-3-Clause
- [LuaHelper Config Docs](https://github.com/Tencent/LuaHelper/blob/master/docs/manual/config.md)
- [VS Code Extension Publishing](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)
- [MCP Registry Publishing](https://modelcontextprotocol.io/registry/quickstart.md)
