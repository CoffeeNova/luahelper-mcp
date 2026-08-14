using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using LuaHelperMcpServer.Services;

namespace LuaHelperMcpServer.Tests.Unit.Helpers;

/// <summary>
/// An in-process fake LSP server that communicates over anonymous pipes.
/// Responds to initialize, initialized, textDocument/didOpen, and shutdown.
/// </summary>
public sealed class FakeLspServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly AnonymousPipeServerStream _clientToServerWrite;
    private readonly AnonymousPipeClientStream _clientToServerRead;
    private readonly AnonymousPipeServerStream _serverToClientWrite;
    private readonly AnonymousPipeClientStream _serverToClientRead;

    public Stream ClientStdin { get; }
    public Stream ClientStdout { get; }

    public FakeLspServer()
    {
        _clientToServerWrite = new AnonymousPipeServerStream(PipeDirection.Out);
        _clientToServerRead = new AnonymousPipeClientStream(
            PipeDirection.In,
            _clientToServerWrite.GetClientHandleAsString()
        );
        _serverToClientWrite = new AnonymousPipeServerStream(PipeDirection.Out);
        _serverToClientRead = new AnonymousPipeClientStream(
            PipeDirection.In,
            _serverToClientWrite.GetClientHandleAsString()
        );

        ClientStdin = _clientToServerWrite;
        ClientStdout = _serverToClientRead;
    }

    public void Start()
    {
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void CloseOutput()
    {
        _serverToClientWrite.Dispose();
    }

    public void SendWindowLogMessage(string message = "fake log message")
    {
        SendResponse(
            new
            {
                jsonrpc = "2.0",
                method = "window/logMessage",
                @params = new { type = 3, message },
            }
        );
    }

    public void SendUnknownNotification(string method = "some/customNotification")
    {
        SendResponse(
            new
            {
                jsonrpc = "2.0",
                method,
                @params = new { payload = 1 },
            }
        );
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var reader = new LspMessageReader(_clientToServerRead);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = await reader.ReadMessageAsync(ct);
                if (message == null)
                    break;
                ProcessMessage(message.Value);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }
        }
    }

    private void ProcessMessage(JsonElement msg)
    {
        if (!msg.TryGetProperty("method", out var methodProp))
            return;

        var method = methodProp.GetString();
        var id = msg.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : (int?)null;

        switch (method)
        {
            case "initialize":
                HandleInitialize(id!.Value);
                break;
            case "textDocument/didOpen":
                HandleDidOpen(msg);
                break;
            case "shutdown":
                HandleShutdown(id!.Value);
                break;
        }
    }

    private void HandleInitialize(int id)
    {
        SendResponse(
            new
            {
                jsonrpc = "2.0",
                id,
                result = new { capabilities = new { textDocumentSync = 1 } },
            }
        );
    }

    private void HandleDidOpen(JsonElement root)
    {
        var uri = string.Empty;
        if (
            root.TryGetProperty("params", out var p)
            && p.TryGetProperty("textDocument", out var td)
            && td.TryGetProperty("uri", out var u)
        )
            uri = u.GetString() ?? string.Empty;

        SendResponse(
            new
            {
                jsonrpc = "2.0",
                method = "textDocument/publishDiagnostics",
                @params = new
                {
                    uri,
                    diagnostics = new[]
                    {
                        new
                        {
                            range = new
                            {
                                start = new { line = 0, character = 0 },
                                end = new { line = 0, character = 5 },
                            },
                            severity = 2,
                            warningType = 1,
                            message = "Test warning from fake server",
                        },
                    },
                },
            }
        );
    }

    private void HandleShutdown(int id)
    {
        SendResponse(
            new
            {
                jsonrpc = "2.0",
                id,
                result = (object?)null,
            }
        );
    }

    private void SendResponse(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        _serverToClientWrite.Write(header, 0, header.Length);
        _serverToClientWrite.Write(bytes, 0, bytes.Length);
        _serverToClientWrite.Flush();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _clientToServerWrite.Dispose();
        _clientToServerRead.Dispose();
        _serverToClientWrite.Dispose();
        _serverToClientRead.Dispose();
    }
}
