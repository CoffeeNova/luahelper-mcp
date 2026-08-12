using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Services;

public interface IDiagnosticCache
{
    void StoreDiagnostics(string uri, List<LuaDiagnostic> diagnostics);
    List<LuaDiagnostic>? GetDiagnostics(string uri);
    IReadOnlyDictionary<string, List<LuaDiagnostic>> GetAllDiagnostics();
    void Clear();
    IEnumerable<string> GetOpenedFileUris();
    void StoreFileContent(string uri, string content);
    string? GetFileContent(string uri);
}
