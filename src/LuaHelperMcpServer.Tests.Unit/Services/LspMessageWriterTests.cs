using System.Text;
using System.Text.Json.Nodes;
using LuaHelperMcpServer.Services;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class LspMessageWriterTests
{
    [Test]
    public async Task SendRequestAsync_WritesCorrectFrame()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        // Act
        await writer.SendRequestAsync(1, "testMethod", new JsonObject { ["foo"] = "bar" });

        // Assert
        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);
        text.ShouldContain("Content-Length:", Case.Sensitive);
        text.ShouldContain("testMethod", Case.Sensitive);
        text.ShouldContain("\"id\":1", Case.Sensitive);
        text.ShouldContain("\"foo\":\"bar\"", Case.Sensitive);
    }

    [Test]
    public async Task SendNotificationAsync_WritesCorrectFrame()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        // Act
        await writer.SendNotificationAsync("notifyMethod", new JsonObject { ["data"] = 42 });

        // Assert
        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);
        text.ShouldContain("Content-Length:", Case.Sensitive);
        text.ShouldContain("notifyMethod", Case.Sensitive);
        text.ShouldNotContain("\"id\"", Case.Sensitive);
        text.ShouldContain("\"data\":42", Case.Sensitive);
    }

    [Test]
    public async Task SendRequestAsync_NullParams_OmitsParamsField()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        // Act
        await writer.SendRequestAsync(2, "nullParams", null);

        // Assert
        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);
        text.ShouldContain("Content-Length:", Case.Sensitive);
        text.ShouldContain("nullParams", Case.Sensitive);
        text.ShouldContain("\"params\":null", Case.Sensitive);
    }

    [Test]
    public async Task ConcurrentWrites_AreSerialized()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        // Act
        var task1 = writer.SendRequestAsync(1, "method1", null);
        var task2 = writer.SendRequestAsync(2, "method2", null);
        await Task.WhenAll(task1, task2);

        // Assert
        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);
        text.ShouldContain("method1", Case.Sensitive);
        text.ShouldContain("method2", Case.Sensitive);

        var messages = ParseFrames(raw);
        messages.Count.ShouldBe(2);
    }

    private static List<string> ParseFrames(byte[] data)
    {
        var result = new List<string>();
        var pos = 0;

        while (pos < data.Length)
        {
            var headerEnd = -1;
            for (var i = pos; i < data.Length - 3; i++)
            {
                if (
                    data[i] == '\r'
                    && data[i + 1] == '\n'
                    && data[i + 2] == '\r'
                    && data[i + 3] == '\n'
                )
                {
                    headerEnd = i;
                    break;
                }
            }
            if (headerEnd == -1)
                break;

            var header = Encoding.ASCII.GetString(data, pos, headerEnd - pos);
            var match = System.Text.RegularExpressions.Regex.Match(
                header,
                @"Content-Length:\s*(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (!match.Success)
                break;

            var length = int.Parse(match.Groups[1].Value);
            var bodyStart = headerEnd + 4;
            if (bodyStart + length > data.Length)
                break;

            result.Add(Encoding.UTF8.GetString(data, bodyStart, length));
            pos = bodyStart + length;
        }

        return result;
    }
}
