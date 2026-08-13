using System.ComponentModel;
using System.Text.Json;
using LuaHelperMcpServer.Services;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Tools;

[McpServerToolType]
public sealed class ConfigTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConfigService _configService;

    public ConfigTools(IConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    [McpServerTool(Name = "get_luahelper_config")]
    [Description(
        "Get the current LuaHelper configuration for a project, including check flags and ignored files."
    )]
    public async Task<string> GetLuahelperConfig(
        [Description("Absolute path to the project root")] string projectPath,
        CancellationToken ct
    )
    {
        if (!Directory.Exists(projectPath))
            return $"Error: Directory not found: {projectPath}";

        var config = await _configService.GetConfig(projectPath, ct);
        return JsonSerializer.Serialize(config, JsonOptions);
    }

    [McpServerTool(Name = "create_luahelper_json")]
    [Description("Create a default luahelper.json configuration file in the project root.")]
    public async Task<string> CreateLuahelperJson(
        [Description("Absolute path to the project root")] string projectPath,
        CancellationToken ct
    )
    {
        if (!Directory.Exists(projectPath))
            return $"Error: Directory not found: {projectPath}";

        return await _configService.CreateDefaultConfig(projectPath, ct);
    }
}
