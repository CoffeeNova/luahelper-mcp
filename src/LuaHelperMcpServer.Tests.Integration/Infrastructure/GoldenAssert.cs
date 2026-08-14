using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace LuaHelperMcpServer.Tests.Integration.Infrastructure;

/// <summary>
/// Golden-file assertions: exact JSON comparison with a readable diff on mismatch.
/// </summary>
public static class GoldenAssert
{
    public static string GoldenPath(string fixtureName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);

    public static string ReadGolden(string fixtureName)
    {
        var path = GoldenPath(fixtureName);
        if (!File.Exists(path))
            Assert.Fail($"Golden file not found: {path}");
        return File.ReadAllText(path);
    }

    public static void AssertJsonEquals(string expectedJson, string actualJson)
    {
        JsonNode? expected;
        JsonNode? actual;
        try
        {
            expected = JsonNode.Parse(expectedJson);
            actual = JsonNode.Parse(actualJson);
        }
        catch (JsonException ex)
        {
            Assert.Fail(
                $"Golden JSON comparison failed to parse:{Environment.NewLine}"
                    + $"Expected: {Truncate(expectedJson)}{Environment.NewLine}"
                    + $"Actual: {Truncate(actualJson)}{Environment.NewLine}"
                    + $"Error: {ex.Message}"
            );
            return;
        }

        if (JsonNode.DeepEquals(expected, actual))
            return;

        var difference = FirstDifference(expected, actual, "$");
        Assert.Fail(
            $"Golden JSON mismatch at {difference?.Path ?? "$"}:{Environment.NewLine}"
                + $"  expected {difference?.Expected ?? "<null>"}{Environment.NewLine}"
                + $"  actual   {difference?.Actual ?? "<null>"}{Environment.NewLine}"
                + $"{Environment.NewLine}Expected:{Environment.NewLine}{Pretty(expected)}{Environment.NewLine}"
                + $"{Environment.NewLine}Actual:{Environment.NewLine}{Pretty(actual)}"
        );
    }

    private static (string Path, string? Expected, string? Actual)? FirstDifference(
        JsonNode? expected,
        JsonNode? actual,
        string path
    )
    {
        if (JsonNode.DeepEquals(expected, actual))
            return null;

        if (expected is JsonObject expectedObject && actual is JsonObject actualObject)
        {
            foreach (
                var key in expectedObject.Select(k => k.Key).Union(actualObject.Select(k => k.Key))
            )
            {
                var childPath = $"{path}.{key}";
                var childDifference = FirstDifference(
                    expectedObject.TryGetPropertyValue(key, out var e) ? e : null,
                    actualObject.TryGetPropertyValue(key, out var a) ? a : null,
                    childPath
                );
                if (childDifference != null)
                    return childDifference;
            }
            return (path, Pretty(expected), Pretty(actual));
        }

        if (expected is JsonArray expectedArray && actual is JsonArray actualArray)
        {
            var count = Math.Max(expectedArray.Count, actualArray.Count);
            for (var i = 0; i < count; i++)
            {
                var childPath = $"{path}[{i}]";
                var childDifference = FirstDifference(
                    i < expectedArray.Count ? expectedArray[i] : null,
                    i < actualArray.Count ? actualArray[i] : null,
                    childPath
                );
                if (childDifference != null)
                    return childDifference;
            }
            return (path, Pretty(expected), Pretty(actual));
        }

        return (path, Pretty(expected), Pretty(actual));
    }

    private static string? Pretty(JsonNode? node) =>
        node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private static string Truncate(string value, int maxLength = 500) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
