using System.Text;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Unit.Helpers;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class LspMessageReaderTests
{
    [Test]
    public async Task ReadMessageAsync_ValidMessage_ReturnsJsonElement()
    {
        // Arrange
        var json = "{\"test\":true}";
        var frame = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame));
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Value.GetProperty("test").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task ReadMessageAsync_EmptyStream_ReturnsNull()
    {
        // Arrange
        var stream = new MemoryStream(Array.Empty<byte>());
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ReadMessageAsync_PartialHeader_ContinuesReading()
    {
        // Arrange
        var json = "{\"msg\":\"hello\"}";
        var header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n";
        var rest = $"\r\n{json}";
        var stream = new PartialWriteStream(header, rest);
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Value.GetProperty("msg").GetString().ShouldBe("hello");
    }

    [Test]
    public async Task ReadMessageAsync_LargeBody_ReadsFully()
    {
        // Arrange
        var largeString = new string('x', 10000);
        var json = $"{{\"data\":\"{largeString}\"}}";
        var frame = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame));
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Value.GetProperty("data").GetString().ShouldBe(largeString);
    }

    [Test]
    public async Task ReadMessageAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var stream = new MemoryStream(Array.Empty<byte>());
        var reader = new LspMessageReader(stream);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var task = reader.ReadMessageAsync(cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(task);
    }

    [Test]
    public async Task ReadMessageAsync_TwoMessagesInBuffer_ReturnsBothInOrder()
    {
        // Arrange
        var json1 = "{\"first\":1}";
        var json2 = "{\"second\":2}";
        var frame1 = $"Content-Length: {Encoding.UTF8.GetByteCount(json1)}\r\n\r\n{json1}";
        var frame2 = $"Content-Length: {Encoding.UTF8.GetByteCount(json2)}\r\n\r\n{json2}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(frame1 + frame2));
        var reader = new LspMessageReader(stream);

        // Act
        var first = await reader.ReadMessageAsync();
        var second = await reader.ReadMessageAsync();

        // Assert
        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first.Value.GetProperty("first").GetInt32().ShouldBe(1);
        second.Value.GetProperty("second").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task ReadMessageAsync_InvalidHeader_SkipsToNextMessage()
    {
        // Arrange — garbage before a valid frame must be skipped
        var json = "{\"ok\":true}";
        var frame = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("NOT-A-HEADER\r\n\r\n" + frame));
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Value.GetProperty("ok").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task ReadMessageAsync_HeaderSplitAcrossReads_StillParses()
    {
        // Arrange — header terminator split between two reads
        var json = "{\"split\":\"header\"}";
        var first = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r";
        var rest = $"\n\r\n{json}";
        var stream = new PartialWriteStream(first, rest);
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Value.GetProperty("split").GetString().ShouldBe("header");
    }

    [Test]
    public async Task ReadMessageAsync_BodySplitAcrossReads_StillParses()
    {
        // Arrange — body split between two reads
        var json = "{\"payload\":\"split-body\"}";
        var header = $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n";
        var splitAt = json.Length / 2;
        var stream = new PartialWriteStream(header + json[..splitAt], json[splitAt..]);
        var reader = new LspMessageReader(stream);

        // Act
        var result = await reader.ReadMessageAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Value.GetProperty("payload").GetString().ShouldBe("split-body");
    }
}
