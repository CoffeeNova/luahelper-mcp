using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class DiagnosticCacheTests
{
    private readonly DiagnosticCache _cache = new();

    [Test]
    public void StoreDiagnostics_GetDiagnostics_ReturnsSameList()
    {
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

        _cache.StoreDiagnostics(uri, diagnostics);
        var result = _cache.GetDiagnostics(uri);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Message, Is.EqualTo("Test warning"));
        Assert.That(result[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    }

    [Test]
    public void GetDiagnostics_NotInCache_ReturnsNull()
    {
        var result = _cache.GetDiagnostics("file:///nonexistent.lua");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetAllDiagnostics_ReturnsAll()
    {
        _cache.StoreDiagnostics(
            "file:///a.lua",
            new List<LuaDiagnostic> { new() { Message = "A" } }
        );
        _cache.StoreDiagnostics(
            "file:///b.lua",
            new List<LuaDiagnostic> { new() { Message = "B" } }
        );

        var all = _cache.GetAllDiagnostics();

        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all.Keys, Does.Contain("file:///a.lua"));
        Assert.That(all.Keys, Does.Contain("file:///b.lua"));
    }

    [Test]
    public void Clear_RemovesAll()
    {
        _cache.StoreDiagnostics(
            "file:///test.lua",
            new List<LuaDiagnostic> { new() { Message = "X" } }
        );
        _cache.StoreFileContent("file:///test.lua", "local x = 1");

        _cache.Clear();

        Assert.That(_cache.GetDiagnostics("file:///test.lua"), Is.Null);
        Assert.That(_cache.GetFileContent("file:///test.lua"), Is.Null);
        Assert.That(_cache.GetAllDiagnostics(), Is.Empty);
    }

    [Test]
    public void StoreFileContent_GetFileContent_RoundTrip()
    {
        var uri = "file:///test.lua";
        var content = "local x = 1";

        _cache.StoreFileContent(uri, content);
        var result = _cache.GetFileContent(uri);

        Assert.That(result, Is.EqualTo(content));
    }

    [Test]
    public void GetOpenedFileUris_ReturnsStoredUris()
    {
        _cache.StoreFileContent("file:///a.lua", "a");
        _cache.StoreFileContent("file:///b.lua", "b");

        var uris = _cache.GetOpenedFileUris().ToList();

        Assert.That(uris, Does.Contain("file:///a.lua"));
        Assert.That(uris, Does.Contain("file:///b.lua"));
        Assert.That(uris, Has.Count.EqualTo(2));
    }
}
