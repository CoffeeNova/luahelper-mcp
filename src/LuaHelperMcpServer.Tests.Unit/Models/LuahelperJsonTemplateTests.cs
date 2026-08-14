using System.Text.Json;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Serialization;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Models;

public class LuahelperJsonTemplateTests
{
    [Test]
    public void Serialize_IndentedJson_HasExpectedDefaults()
    {
        // Arrange
        var template = new LuahelperJsonTemplate();

        // Act
        var json = JsonSerializer.Serialize(template, LspJson.Indented.LuahelperJsonTemplate);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Assert
        root.GetProperty("BaseDir").GetString().ShouldBe("./");
        root.GetProperty("ShowWarnFlag").GetInt32().ShouldBe(1);
        root.GetProperty("PathSeparator").GetString().ShouldBe(".");
        root.GetProperty("IgnoreModules")
            .EnumerateArray()
            .First()
            .GetString()
            .ShouldBe("C_Container");
        root.GetProperty("IgnoreFileOrFloder")
            .EnumerateArray()
            .First()
            .GetString()
            .ShouldBe(".vscode/");
        root.GetProperty("ProjectFiles").GetArrayLength().ShouldBe(0);
    }
}
