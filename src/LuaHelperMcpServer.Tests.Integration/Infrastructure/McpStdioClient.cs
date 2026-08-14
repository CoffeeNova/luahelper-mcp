using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace LuaHelperMcpServer.Tests.Integration.Infrastructure;

/// <summary>
/// Spawns the real LuaHelperMcpServer process and speaks newline-delimited
/// JSON-RPC over stdio (the MCP stdio transport framing — NOT Content-Length).
/// </summary>
public sealed class McpStdioClient : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly List<string> _stderrLines = [];
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly TimeSpan _timeout;
    private readonly object _idLock = new();
    private int _nextId = 1;

    public IReadOnlyList<string> StderrLines => _stderrLines;

    public McpStdioClient(
        string serverCommand,
        string? serverArguments,
        string lualspPath,
        string workingDir,
        TimeSpan? timeout = null
    )
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(60);

        var startInfo = new ProcessStartInfo
        {
            FileName = serverCommand,
            Arguments = serverArguments ?? string.Empty,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment["LUAHELPER_LUALSP_PATH"] = lualspPath;

        _process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Failed to start MCP server process: {serverCommand} {serverArguments}"
            );

        _stdin = new StreamWriter(
            _process.StandardInput.BaseStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 4096,
            leaveOpen: false
        )
        {
            AutoFlush = true,
        };

        _ = Task.Run(() => ReadStdoutLoopAsync());
        _ = Task.Run(() => DrainStderrAsync());
    }

    public Task<JsonNode> InitializeAsync(CancellationToken ct = default)
    {
        var initParams = new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject
            {
                ["name"] = "luahelper-mcp-integration-tests",
                ["version"] = "1.0.0",
            },
        };
        var initialize = SendRequestAsync("initialize", initParams, ct);
        return SendNotificationAsync(new JsonObject(), ct)
            .ContinueWith(_ => initialize, ct, TaskContinuationOptions.None, TaskScheduler.Default)
            .Unwrap();
    }

    public Task<JsonNode> CallAsync(
        string method,
        JsonNode? @params = null,
        CancellationToken ct = default
    ) => SendRequestAsync(method, @params, ct);

    public Task<JsonNode> CallToolAsync(
        string toolName,
        JsonNode arguments,
        CancellationToken ct = default
    ) =>
        SendRequestAsync(
            "tools/call",
            new JsonObject { ["name"] = toolName, ["arguments"] = arguments },
            ct
        );

    public Task<JsonNode> ReadResourceAsync(string uri, CancellationToken ct = default) =>
        SendRequestAsync("resources/read", new JsonObject { ["uri"] = uri }, ct);

    public Task<JsonNode> GetPromptAsync(
        string name,
        JsonNode arguments,
        CancellationToken ct = default
    ) =>
        SendRequestAsync(
            "prompts/get",
            new JsonObject { ["name"] = name, ["arguments"] = arguments },
            ct
        );

    private async Task<JsonNode> SendRequestAsync(
        string method,
        JsonNode? @params,
        CancellationToken ct
    )
    {
        int id;
        lock (_idLock)
        {
            id = _nextId++;
        }

        var tcs = new TaskCompletionSource<JsonNode>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pending[id] = tcs;

        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = @params,
        };

        try
        {
            await WriteLineAsync(message.ToJsonString(), ct);
            return await tcs.Task.WaitAsync(_timeout, ct);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SendNotificationAsync(JsonNode? @params, CancellationToken ct)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized",
            ["params"] = @params,
        };
        await WriteLineAsync(message.ToJsonString(), ct);
    }

    private async Task WriteLineAsync(string line, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await _stdin.WriteLineAsync(line);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadStdoutLoopAsync()
    {
        try
        {
            var reader = new StreamReader(
                _process.StandardOutput.BaseStream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false
            );
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                JsonNode? message;
                try
                {
                    message = JsonNode.Parse(line);
                }
                catch (Exception)
                {
                    continue;
                }

                if (
                    message?["id"] is not JsonValue idValue
                    || !idValue.TryGetValue<int>(out var id)
                )
                    continue;

                if (_pending.TryRemove(id, out var tcs))
                    tcs.TrySetResult(message);
            }
        }
        catch (Exception)
        {
            // Process died mid-request; pending calls are failed on dispose.
        }
    }

    private async Task DrainStderrAsync()
    {
        try
        {
            var reader = new StreamReader(
                _process.StandardError.BaseStream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: false
            );
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;
                lock (_stderrLines)
                {
                    _stderrLines.Add(line);
                }
            }
        }
        catch (Exception)
        {
            // Ignored; the stderr drain must never crash the test.
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception)
        {
            // Already dead or killed concurrently.
        }

        _process.Dispose();
        _writeLock.Dispose();

        foreach (var tcs in _pending.Values)
            tcs.TrySetCanceled();
        _pending.Clear();
    }
}
