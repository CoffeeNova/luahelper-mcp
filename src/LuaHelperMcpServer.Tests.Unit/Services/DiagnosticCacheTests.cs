using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class DiagnosticCacheTests
{
    private readonly DiagnosticCache _cache = new();

    [Test]
    public void StoreDiagnostics_GetDiagnostics_ReturnsSameList()
    {
        // Arrange
        var uri = "file:///test.lua";
        var diagnostics = new List<LuaDiagnostic>
        {
            new()
            {
                Uri = uri,
                Message = "Test warning",
                Severity = DiagnosticSeverity.Warning,
            },
        };

        // Act
        _cache.StoreDiagnostics(uri, diagnostics);
        var result = _cache.GetDiagnostics(uri);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Message.ShouldBe("Test warning");
        result[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Test]
    public void GetDiagnostics_NotInCache_ReturnsNull()
    {
        // Act
        var result = _cache.GetDiagnostics("file:///nonexistent.lua");

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public void GetAllDiagnostics_ReturnsAll()
    {
        // Arrange
        _cache.StoreDiagnostics(
            "file:///a.lua",
            new List<LuaDiagnostic> { new() { Message = "A" } }
        );
        _cache.StoreDiagnostics(
            "file:///b.lua",
            new List<LuaDiagnostic> { new() { Message = "B" } }
        );

        // Act
        var all = _cache.GetAllDiagnostics();

        // Assert
        all.Count.ShouldBe(2);
        all.ShouldContainKey("file:///a.lua");
        all.ShouldContainKey("file:///b.lua");
    }

    [Test]
    public void Clear_RemovesAll()
    {
        // Arrange
        _cache.StoreDiagnostics(
            "file:///test.lua",
            new List<LuaDiagnostic> { new() { Message = "X" } }
        );
        _cache.StoreFileContent("file:///test.lua", "local x = 1");

        // Act
        _cache.Clear();

        // Assert
        _cache.GetDiagnostics("file:///test.lua").ShouldBeNull();
        _cache.GetFileContent("file:///test.lua").ShouldBeNull();
        _cache.GetAllDiagnostics().ShouldBeEmpty();
    }

    [Test]
    public void StoreFileContent_GetFileContent_RoundTrip()
    {
        // Arrange
        var uri = "file:///test.lua";
        var content = "local x = 1";

        // Act
        _cache.StoreFileContent(uri, content);
        var result = _cache.GetFileContent(uri);

        // Assert
        result.ShouldBe(content);
    }

    [Test]
    public void GetOpenedFileUris_ReturnsStoredUris()
    {
        // Arrange
        _cache.StoreFileContent("file:///a.lua", "a");
        _cache.StoreFileContent("file:///b.lua", "b");

        // Act
        var uris = _cache.GetOpenedFileUris().ToList();

        // Assert
        uris.ShouldContain("file:///a.lua");
        uris.ShouldContain("file:///b.lua");
        uris.Count.ShouldBe(2);
    }
}
