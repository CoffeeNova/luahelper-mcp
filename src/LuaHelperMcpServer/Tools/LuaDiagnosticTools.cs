using System.ComponentModel;
using System.Text;
using System.Text.Json;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Serialization;
using LuaHelperMcpServer.Services;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Tools;

[McpServerToolType]
public sealed class LuaDiagnosticTools
{
    private static readonly SupportedCheck[] SupportedChecks =
    [
        new()
        {
            Id = 1,
            Name = "Syntax errors",
            DefaultOn = true,
        },
        new()
        {
            Id = 2,
            Name = "Variable not defined",
            DefaultOn = false,
        },
        new()
        {
            Id = 3,
            Name = "Global used before defined",
            DefaultOn = false,
        },
        new()
        {
            Id = 4,
            Name = "Local defined but not used",
            DefaultOn = false,
        },
        new()
        {
            Id = 5,
            Name = "Duplicate table keys",
            DefaultOn = true,
        },
        new()
        {
            Id = 6,
            Name = "Referenced file not found",
            DefaultOn = false,
        },
        new()
        {
            Id = 7,
            Name = "Assignment param count mismatch",
            DefaultOn = true,
        },
        new()
        {
            Id = 8,
            Name = "Local definition param count mismatch",
            DefaultOn = true,
        },
        new()
        {
            Id = 9,
            Name = "Goto label not found",
            DefaultOn = true,
        },
        new()
        {
            Id = 10,
            Name = "Function call param count > definition",
            DefaultOn = false,
        },
        new()
        {
            Id = 11,
            Name = "Import module var not defined",
            DefaultOn = false,
        },
        new()
        {
            Id = 12,
            Name = "If-not block error",
            DefaultOn = false,
        },
        new()
        {
            Id = 13,
            Name = "Duplicate function params",
            DefaultOn = true,
        },
        new()
        {
            Id = 14,
            Name = "Duplicate binary expression",
            DefaultOn = false,
        },
        new()
        {
            Id = 15,
            Name = "OR always true",
            DefaultOn = false,
        },
        new()
        {
            Id = 16,
            Name = "AND always false",
            DefaultOn = false,
        },
        new()
        {
            Id = 17,
            Name = "Unused assignment",
            DefaultOn = false,
        },
        new()
        {
            Id = 18,
            Name = "Annotation type warnings",
            DefaultOn = true,
        },
        new()
        {
            Id = 19,
            Name = "Duplicate if conditions",
            DefaultOn = true,
        },
        new()
        {
            Id = 20,
            Name = "Self-assignment",
            DefaultOn = false,
        },
        new()
        {
            Id = 21,
            Name = "Float equality",
            DefaultOn = false,
        },
    ];

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

        var config = await _configService.GetConfig(Path.GetDirectoryName(filePath)!, ct);
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

        var config = await _configService.GetConfig(projectPath, ct);
        await _lspClient.EnsureInitializedAsync(projectPath, config, ct);

        var luaFiles = Directory
            .EnumerateFiles(projectPath, "*.lua", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\.vscode\\") && !f.Contains("\\Tests\\"))
            .ToList();

        foreach (var file in luaFiles)
            await _lspClient.OpenFileAsync(file, ct);

        var diagnosticTasks = luaFiles
            .Select(file => _lspClient.GetDiagnosticsAsync(file, ct))
            .ToArray();
        var diagnostics = await Task.WhenAll(diagnosticTasks)
            .WaitAsync(TimeSpan.FromSeconds(30), ct);

        var byFile = new Dictionary<string, List<LuaDiagnostic>>();
        for (var i = 0; i < luaFiles.Count; i++)
            byFile[LspClient.PathToUri(luaFiles[i])] = diagnostics[i];

        var collection = new DiagnosticCollection { ProjectPath = projectPath, ByFile = byFile };
        return collection.ToFormattedString();
    }

    [McpServerTool(Name = "get_supported_checks")]
    [Description(
        "List all available LuaHelper check types with their IDs, names, and whether they are enabled by default."
    )]
    public Task<string> GetSupportedChecks(CancellationToken ct)
    {
        return Task.FromResult(
            JsonSerializer.Serialize(SupportedChecks, LspJson.IndentedCamelCase.SupportedCheckArray)
        );
    }

    [McpServerTool(Name = "get_luahelper_version")]
    [Description("Get the version of the bundled lualsp.exe binary.")]
    public Task<string> GetLuahelperVersion(CancellationToken ct)
    {
        return Task.FromResult(_configService.GetVersion());
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
