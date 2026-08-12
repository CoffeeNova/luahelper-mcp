using System.ComponentModel;
using System.Text;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Tools;

[McpServerToolType]
public sealed class LuaDiagnosticTools
{
    private readonly ILspClient _lspClient;
    private readonly IDiagnosticCache _cache;
    private readonly IConfigService _configService;

    public LuaDiagnosticTools(
        ILspClient lspClient,
        IDiagnosticCache cache,
        IConfigService configService
    )
    {
        _lspClient = lspClient ?? throw new ArgumentNullException(nameof(lspClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    [McpServerTool(Name = "check_lua_file")]
    [Description(
        "Run LuaHelper diagnostics on a single .lua file. Returns warnings and errors with line numbers."
    )]
    public async Task<string> CheckLuaFile(
        [Description("Absolute path to the .lua file to check")] string filePath,
        CancellationToken ct
    )
    {
        if (!File.Exists(filePath))
            return $"Error: File not found: {filePath}";

        var config = _configService.GetConfig(Path.GetDirectoryName(filePath)!);
        await _lspClient.EnsureInitializedAsync(config.ProjectPath, config, ct);
        await _lspClient.OpenFileAsync(filePath, ct);
        var diagnostics = await _lspClient.GetDiagnosticsAsync(filePath, ct);

        return FormatDiagnostics(filePath, diagnostics);
    }

    [McpServerTool(Name = "check_lua_project")]
    [Description(
        "Run LuaHelper diagnostics on an entire Lua project. Scans all .lua files recursively."
    )]
    public async Task<string> CheckLuaProject(
        [Description("Absolute path to the project root directory")] string projectPath,
        CancellationToken ct
    )
    {
        if (!Directory.Exists(projectPath))
            return $"Error: Directory not found: {projectPath}";

        var config = _configService.GetConfig(projectPath);
        await _lspClient.EnsureInitializedAsync(projectPath, config, ct);

        var luaFiles = Directory
            .EnumerateFiles(projectPath, "*.lua", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\.vscode\\") && !f.Contains("\\Tests\\"))
            .ToList();

        foreach (var file in luaFiles)
            await _lspClient.OpenFileAsync(file, ct);

        // Wait for diagnostics to arrive (improved in Phase 2)
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        var allDiags = _lspClient.GetAllDiagnostics();
        var collection = new DiagnosticCollection
        {
            ProjectPath = projectPath,
            ByFile = allDiags.ToDictionary(kv => kv.Key, kv => kv.Value),
        };
        return collection.ToFormattedString();
    }

    private static string FormatDiagnostics(string filePath, List<LuaDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
            return $"No warnings found in {filePath}";

        var sb = new StringBuilder();
        sb.AppendLine($"{diagnostics.Count} warning(s) in {filePath}:");
        foreach (var d in diagnostics)
            sb.AppendLine($"  L{d.StartLine}:{d.StartCharacter} [{d.Severity}] {d.Message}");
        return sb.ToString();
    }
}
