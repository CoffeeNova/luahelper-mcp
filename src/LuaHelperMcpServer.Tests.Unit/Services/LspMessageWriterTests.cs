using System.Text;
using LuaHelperMcpServer.Services;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class LspMessageWriterTests
{
    [Test]
    public async Task SendRequestAsync_WritesCorrectFrame()
    {
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        await writer.SendRequestAsync(1, "testMethod", new { foo = "bar" });

        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);

        Assert.That(text, Does.Contain("Content-Length:"));
        Assert.That(text, Does.Contain("testMethod"));
        Assert.That(text, Does.Contain("\"id\":1"));
        Assert.That(text, Does.Contain("\"foo\":\"bar\""));
    }

    [Test]
    public async Task SendNotificationAsync_WritesCorrectFrame()
    {
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        await writer.SendNotificationAsync("notifyMethod", new { data = 42 });

        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);

        Assert.That(text, Does.Contain("Content-Length:"));
        Assert.That(text, Does.Contain("notifyMethod"));
        Assert.That(text, Does.Not.Contain("\"id\""));
        Assert.That(text, Does.Contain("\"data\":42"));
    }

    [Test]
    public async Task SendRequestAsync_NullParams_OmitsParamsField()
    {
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        await writer.SendRequestAsync(2, "nullParams", null);

        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);

        Assert.That(text, Does.Contain("Content-Length:"));
        Assert.That(text, Does.Contain("nullParams"));
        Assert.That(text, Does.Contain("\"params\":null"));
    }

    [Test]
    public async Task ConcurrentWrites_AreSerialized()
    {
        using var stream = new MemoryStream();
        var writer = new LspMessageWriter(stream);

        var task1 = writer.SendRequestAsync(1, "method1", null);
        var task2 = writer.SendRequestAsync(2, "method2", null);
        await Task.WhenAll(task1, task2);

        var raw = stream.ToArray();
        var text = Encoding.UTF8.GetString(raw);

        Assert.That(text, Does.Contain("method1"));
        Assert.That(text, Does.Contain("method2"));

        var messages = ParseFrames(raw);
        Assert.That(messages, Has.Count.EqualTo(2));
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
