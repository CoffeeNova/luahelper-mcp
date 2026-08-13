using System.Text;
using System.Text.Json;

namespace LuaHelperMcpServer.Services;

public sealed class LspMessageWriter
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public LspMessageWriter(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task SendRequestAsync(
        int id,
        string method,
        object? parameters,
        CancellationToken ct = default
    )
    {
        var bodyObj = new Dictionary<string, object?>
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
        object? parameters,
        CancellationToken ct = default
    )
    {
        var bodyObj = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters,
        };

        await SendAsync(bodyObj, ct);
    }

    private async Task SendAsync(object bodyObj, CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(bodyObj, JsonOptions);
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
