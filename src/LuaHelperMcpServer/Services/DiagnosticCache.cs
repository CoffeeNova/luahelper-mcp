using System.Collections.Concurrent;
using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Services;

public sealed class DiagnosticCache : IDiagnosticCache
{
    private readonly ConcurrentDictionary<string, List<LuaDiagnostic>> _diagnostics = new();
    private readonly ConcurrentDictionary<string, string> _fileContents = new();

    public void StoreDiagnostics(string uri, List<LuaDiagnostic> diagnostics)
    {
        _diagnostics[uri] = diagnostics;
    }

    public List<LuaDiagnostic>? GetDiagnostics(string uri)
    {
        return _diagnostics.TryGetValue(uri, out var diags) ? diags : null;
    }

    public IReadOnlyDictionary<string, List<LuaDiagnostic>> GetAllDiagnostics()
    {
        return _diagnostics;
    }

    public void Clear()
    {
        _diagnostics.Clear();
        _fileContents.Clear();
    }

    public IEnumerable<string> GetOpenedFileUris()
    {
        return _fileContents.Keys;
    }

    public void StoreFileContent(string uri, string content)
    {
        _fileContents[uri] = content;
    }

    public string? GetFileContent(string uri)
    {
        return _fileContents.TryGetValue(uri, out var content) ? content : null;
    }
}
