using LuaHelperMcpServer.Models;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Models;

public class DiagnosticCollectionTests
{
    [Test]
    public void ToFormattedString_Empty_ReturnsNoWarningsFound()
    {
        // Arrange
        var collection = new DiagnosticCollection { ProjectPath = "C:\\project" };

        // Act
        var result = collection.ToFormattedString();

        // Assert
        result.ShouldBe("No warnings found in project C:\\project");
    }

    [Test]
    public void ToFormattedString_WithDiagnostics_ListsFilesAndDiagnostics()
    {
        // Arrange
        var collection = new DiagnosticCollection
        {
            ProjectPath = "C:\\project",
            ByFile = new Dictionary<string, List<LuaDiagnostic>>
            {
                ["file:///C:/project/src/main.lua"] =
                [
                    new LuaDiagnostic
                    {
                        StartLine = 3,
                        StartCharacter = 5,
                        Severity = DiagnosticSeverity.Warning,
                        Message = "local defined but not used",
                    },
                ],
                ["file:///C:/project/src/empty.lua"] = [],
            },
        };

        // Act
        var result = collection.ToFormattedString();

        // Assert
        result.ShouldContain("Project C:\\project: 1 warning(s) across 1 file(s)", Case.Sensitive);
        result.ShouldContain("--- C:\\project\\src\\main.lua (1) ---", Case.Sensitive);
        result.ShouldContain("  L3:5 [Warning] local defined but not used", Case.Sensitive);
    }

    [Test]
    public void TotalCount_CountsAllDiagnostics()
    {
        // Arrange
        var collection = new DiagnosticCollection
        {
            ByFile = new Dictionary<string, List<LuaDiagnostic>>
            {
                ["a"] = [new LuaDiagnostic(), new LuaDiagnostic()],
                ["b"] = [new LuaDiagnostic()],
                ["c"] = [],
            },
        };

        // Act
        var total = collection.TotalCount;

        // Assert
        total.ShouldBe(3);
    }
}
