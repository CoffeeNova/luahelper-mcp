using LuaHelperMcpServer.Models;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Models;

public class LuaDiagnosticTests
{
    [Test]
    public void ToFormattedString_FormatsLineCharSeverityMessage()
    {
        // Arrange
        var diagnostic = new LuaDiagnostic
        {
            StartLine = 10,
            StartCharacter = 4,
            Severity = DiagnosticSeverity.Error,
            Message = "syntax error",
        };

        // Act
        var result = diagnostic.ToFormattedString();

        // Assert
        result.ShouldBe("L10:4 [Error] syntax error");
    }

    [Test]
    public void Defaults_AreEmptyAndZero()
    {
        // Arrange & Act
        var diagnostic = new LuaDiagnostic();

        // Assert
        diagnostic.Uri.ShouldBeEmpty();
        diagnostic.Message.ShouldBeEmpty();
        diagnostic.Source.ShouldBeNull();
        diagnostic.StartLine.ShouldBe(0);
        diagnostic.StartCharacter.ShouldBe(0);
        diagnostic.EndLine.ShouldBe(0);
        diagnostic.EndCharacter.ShouldBe(0);
        diagnostic.WarningType.ShouldBe(0);
    }
}
