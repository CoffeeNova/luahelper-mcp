using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LuaHelperMcpServer.Serialization;

namespace LuaHelperMcpServer.Services;

public sealed class LspMessageWriter
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public LspMessageWriter(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task SendRequestAsync(
        int id,
        string method,
        JsonNode? parameters,
        CancellationToken ct = default
    )
    {
        var bodyObj = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };

        await SendAsync(bodyObj, ct);
    }

    public async Task SendNotificationAsync(
        string method,
        JsonNode? parameters,
        CancellationToken ct = default
    )
    {
        var bodyObj = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters,
        };

        await SendAsync(bodyObj, ct);
    }

    private async Task SendAsync(JsonObject bodyObj, CancellationToken ct)
    {
        byte[] body;
        using (var stream = new MemoryStream())
        {
            using (var jsonWriter = new Utf8JsonWriter(stream))
            {
                bodyObj.WriteTo(jsonWriter);
            }
            body = stream.ToArray();
        }

        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

        await _writeLock.WaitAsync(ct);
        try
        {
            await _stream.WriteAsync(header, ct);
            await _stream.WriteAsync(body, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
