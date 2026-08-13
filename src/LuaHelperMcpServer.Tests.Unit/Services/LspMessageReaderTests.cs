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
        async Task Act() => await reader.ReadMessageAsync(cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(Act);
    }
}
