using System.ComponentModel;
using System.Text.Json;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Serialization;
using LuaHelperMcpServer.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Resources;

[McpServerResourceType]
public sealed class DiagnosticResources
{
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
            return JsonSerializer.Serialize(cached, LspJson.IndentedCamelCase.ListLuaDiagnostic);

        var config = await _configService.GetConfig(Path.GetDirectoryName(filePath)!, ct);
        await EnsureLspReadyAsync(config.ProjectPath, config, ct);
        await _lspClient.OpenFileAsync(filePath, ct);
        List<LuaDiagnostic> diagnostics;
        try
        {
            diagnostics = await _lspClient
                .GetDiagnosticsAsync(filePath, ct)
                .WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
        catch (TimeoutException)
        {
            diagnostics = [];
        }

        return JsonSerializer.Serialize(diagnostics, LspJson.IndentedCamelCase.ListLuaDiagnostic);
    }

    private async Task EnsureLspReadyAsync(string projectPath, LuaHelperConfig config, CancellationToken ct)
    {
        try
        {
            await _lspClient.EnsureInitializedAsync(projectPath, config, ct);
        }
        catch (InvalidOperationException) when (
            _lspClient.State == LspState.Crashed || _lspClient.State == LspState.Failed
        )
        {
            await _lspClient.ShutdownAsync(ct);
            await _lspClient.EnsureInitializedAsync(projectPath, config, ct);
        }
    }

    [McpServerResource(
        UriTemplate = "luahelper://config",
        Name = "Config",
        Title = "LuaHelper configuration",
        MimeType = "application/json"
    )]
    [Description("Returns the current LuaHelper configuration for the active project as JSON.")]
    public async Task<string> GetConfig(CancellationToken ct)
    {
        var config = string.IsNullOrEmpty(_lspClient.ProjectPath)
            ? new LuaHelperConfig()
            : await _configService.GetConfig(_lspClient.ProjectPath!, ct);
        return JsonSerializer.Serialize(config, LspJson.IndentedCamelCase.LuaHelperConfig);
    }
}
