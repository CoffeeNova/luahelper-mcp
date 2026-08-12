using System.ComponentModel;
using System.Text.Json;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Resources;

[McpServerResourceType]
public sealed class DiagnosticResources
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ILspClient _lspClient;
    private readonly IDiagnosticCache _cache;
    private readonly IConfigService _configService;

    public DiagnosticResources(
        ILspClient lspClient,
        IDiagnosticCache cache,
        IConfigService configService
    )
    {
        _lspClient = lspClient ?? throw new ArgumentNullException(nameof(lspClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    [McpServerResource(
        UriTemplate = "luahelper://diagnostics/{+filePath}",
        Name = "Diagnostics",
        Title = "Lua file diagnostics",
        MimeType = "application/json"
    )]
    [Description("Returns the current LuaHelper diagnostics for a Lua file as JSON.")]
    public async Task<string> GetDiagnostics(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            throw new McpException($"File not found: {filePath}");

        var uri = LspClient.PathToUri(filePath);
        var cached = _cache.GetDiagnostics(uri);
        if (cached != null)
            return JsonSerializer.Serialize(cached, JsonOptions);

        var config = _configService.GetConfig(Path.GetDirectoryName(filePath)!);
        await _lspClient.EnsureInitializedAsync(config.ProjectPath, config, ct);
        await _lspClient.OpenFileAsync(filePath, ct);
        var diagnostics = await _lspClient.GetDiagnosticsAsync(filePath, ct);

        return JsonSerializer.Serialize(diagnostics, JsonOptions);
    }

    [McpServerResource(
        UriTemplate = "luahelper://config",
        Name = "Config",
        Title = "LuaHelper configuration",
        MimeType = "application/json"
    )]
    [Description("Returns the current LuaHelper configuration for the active project as JSON.")]
    public Task<string> GetConfig(CancellationToken ct)
    {
        var config = string.IsNullOrEmpty(_lspClient.ProjectPath)
            ? new LuaHelperConfig()
            : _configService.GetConfig(_lspClient.ProjectPath!);
        return Task.FromResult(JsonSerializer.Serialize(config, JsonOptions));
    }
}
