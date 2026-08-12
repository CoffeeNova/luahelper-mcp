using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Services;

public interface ILspClient
{
    LspState State { get; }
    Task EnsureInitializedAsync(
        string projectPath,
        LuaHelperConfig config,
        CancellationToken ct = default
    );
    Task OpenFileAsync(string filePath, CancellationToken ct = default);
    Task<List<LuaDiagnostic>> GetDiagnosticsAsync(string filePath, CancellationToken ct = default);
    IReadOnlyDictionary<string, List<LuaDiagnostic>> GetAllDiagnostics();
    Task ShutdownAsync(CancellationToken ct = default);
}
