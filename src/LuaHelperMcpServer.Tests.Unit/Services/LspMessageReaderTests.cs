using System.Text;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Unit.Helpers;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class LspMessageReaderTests
{
    [Test]
    public async Task ReadMessageAsync_ValidMessage_ReturnsJsonElement()
    {
        var json = "{\"test\":true}";
        var frame = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame));
        var reader = new LspMessageReader(stream);

        var result = await reader.ReadMessageAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.GetProperty("test").GetBoolean(), Is.True);
    }

    [Test]
    public async Task ReadMessageAsync_EmptyStream_ReturnsNull()
    {
        var stream = new MemoryStream(Array.Empty<byte>());
        var reader = new LspMessageReader(stream);

        var result = await reader.ReadMessageAsync();

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ReadMessageAsync_PartialHeader_ContinuesReading()
    {
        var json = "{\"msg\":\"hello\"}";
        var header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n";
        var rest = $"\r\n{json}";
        var stream = new PartialWriteStream(header, rest);
        var reader = new LspMessageReader(stream);

        var result = await reader.ReadMessageAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.GetProperty("msg").GetString(), Is.EqualTo("hello"));
    }

    [Test]
    public async Task ReadMessageAsync_LargeBody_ReadsFully()
    {
        var largeString = new string('x', 10000);
        var json = $"{{\"data\":\"{largeString}\"}}";
        var frame = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame));
        var reader = new LspMessageReader(stream);

        var result = await reader.ReadMessageAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.GetProperty("data").GetString(), Is.EqualTo(largeString));
    }

    [Test]
    public void ReadMessageAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        var stream = new MemoryStream(Array.Empty<byte>());
        var reader = new LspMessageReader(stream);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await reader.ReadMessageAsync(cts.Token)
        );
    }
}
