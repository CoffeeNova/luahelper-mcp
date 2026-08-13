using System.Text;
using System.Text.Json;

namespace LuaHelperMcpServer.Services;

public sealed class LspMessageReader
{
    private readonly Stream _stream;
    private readonly List<byte> _buffer = new(capacity: 8192);

    public LspMessageReader(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task<JsonElement?> ReadMessageAsync(CancellationToken ct = default)
    {
        var tempBuffer = new byte[8192];

        while (true)
        {
            // Try to find a complete message in the current buffer
            if (TryParseMessage(out var result))
                return result;

            // Read more data from the stream
            var bytesRead = await _stream.ReadAsync(tempBuffer.AsMemory(0, tempBuffer.Length), ct);
            if (bytesRead == 0)
            {
                // Stream ended
                return null;
            }
            _buffer.AddRange(tempBuffer.AsSpan(0, bytesRead));
        }
    }

    private bool TryParseMessage(out JsonElement? result)
    {
        result = null;

        // Find the header terminator \r\n\r\n
        var headerEnd = -1;
        for (var i = 0; i < _buffer.Count - 3; i++)
        {
            if (
                _buffer[i] == '\r'
                && _buffer[i + 1] == '\n'
                && _buffer[i + 2] == '\r'
                && _buffer[i + 3] == '\n'
            )
            {
                headerEnd = i;
                break;
            }
        }

        if (headerEnd == -1)
            return false; // Need more data

        // Parse headers
        var headerSpan = _buffer.GetRange(0, headerEnd).ToArray();
        var headerText = Encoding.ASCII.GetString(headerSpan);
        var contentLength = ParseContentLength(headerText);
        if (contentLength == null)
        {
            // Invalid header — skip to next
            _buffer.RemoveRange(0, headerEnd + 4);
            return TryParseMessage(out result);
        }

        // Check if we have the full body
        var bodyStart = headerEnd + 4;
        var bodyEnd = bodyStart + contentLength.Value;
        if (_buffer.Count < bodyEnd)
            return false; // Need more data

        // Parse the JSON body
        var bodyBytes = _buffer.GetRange(bodyStart, contentLength.Value).ToArray();
        var body = Encoding.UTF8.GetString(bodyBytes);
        result = JsonSerializer.Deserialize<JsonElement>(body);
        _buffer.RemoveRange(0, bodyEnd);
        return true;
    }

    private static int? ParseContentLength(string headerText)
    {
        foreach (var line in headerText.Split('\n'))
        {
            var trimmed = line.Trim('\r', ' ');
            if (trimmed.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.AsSpan("Content-Length:".Length).Trim();
                if (int.TryParse(value, out var length))
                    return length;
            }
        }
        return null;
    }
}
