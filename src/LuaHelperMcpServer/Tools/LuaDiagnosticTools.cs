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
    private readonly IConfigService _configService;

    public LuaDiagnosticTools(ILspClient lspClient, IConfigService configService)
    {
        _lspClient = lspClient ?? throw new ArgumentNullException(nameof(lspClient));
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

        var projectPath = Path.GetDirectoryName(filePath)!;
        var config = await _configService.GetConfig(projectPath, ct);
        await EnsureLspReadyAsync(projectPath, config, ct);
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
        await EnsureLspReadyAsync(projectPath, config, ct);

        var ignoreDirs = BuildIgnoreSet(config.IgnoreFileOrDir);
        var luaFiles = Directory
            .EnumerateFiles(projectPath, "*.lua", SearchOption.AllDirectories)
            .Where(f => !IsIgnoredFile(f, ignoreDirs))
            .ToList();

        foreach (var file in luaFiles)
            await _lspClient.OpenFileAsync(file, ct);

        var results = await Task.WhenAll(
            luaFiles.Select(file => CollectProjectFileDiagnosticsAsync(file, ct))
        );
        var byFile = results.ToDictionary(result => result.Key, result => result.Value);

        var collection = new DiagnosticCollection { ProjectPath = projectPath, ByFile = byFile };
        return collection.ToFormattedString();
    }

    private async Task<
        KeyValuePair<string, List<LuaDiagnostic>>
    > CollectProjectFileDiagnosticsAsync(string file, CancellationToken ct)
    {
        try
        {
            var diagnostics = await _lspClient
                .GetDiagnosticsAsync(file, ct)
                .WaitAsync(TimeSpan.FromSeconds(10), ct);
            return new KeyValuePair<string, List<LuaDiagnostic>>(
                LspClient.PathToUri(file),
                diagnostics
            );
        }
        catch (TimeoutException)
        {
            return new KeyValuePair<string, List<LuaDiagnostic>>(LspClient.PathToUri(file), []);
        }
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

    private async Task EnsureLspReadyAsync(
        string projectPath,
        LuaHelperConfig config,
        CancellationToken ct
    )
    {
        try
        {
            await _lspClient.EnsureInitializedAsync(projectPath, config, ct);
        }
        catch (InvalidOperationException)
            when (_lspClient.State == LspState.Crashed || _lspClient.State == LspState.Failed)
        {
            await _lspClient.ShutdownAsync(ct);
            await _lspClient.EnsureInitializedAsync(projectPath, config, ct);
        }
    }

    internal static HashSet<string> BuildIgnoreSet(List<string>? ignoreFileOrDir)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ignoreFileOrDir ?? [])
        {
            var trimmed = entry.Trim().TrimEnd('/', '\\');
            if (trimmed.Length > 0)
                set.Add(trimmed);
        }

        return set;
    }

    internal static bool IsIgnoredFile(string filePath, HashSet<string> ignoreDirs)
    {
        foreach (var ignore in ignoreDirs)
        {
            if (filePath.IndexOf(ignore, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
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
