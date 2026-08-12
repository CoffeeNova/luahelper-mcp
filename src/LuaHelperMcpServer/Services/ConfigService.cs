using System.Diagnostics;
using System.Text.Json;
using LuaHelperMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace LuaHelperMcpServer.Services;

public sealed class ConfigService : IConfigService
{
    private const string DefaultVersion = "v0.2.29";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _lualspPath;
    private readonly ILogger<ConfigService> _logger;

    public ConfigService(string lualspPath, ILogger<ConfigService> logger)
    {
        _lualspPath = lualspPath ?? throw new ArgumentNullException(nameof(lualspPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public LuaHelperConfig GetConfig(string projectPath)
    {
        return new LuaHelperConfig
        {
            ProjectPath = projectPath,
            PluginPath = Path.GetDirectoryName(_lualspPath) ?? string.Empty,
            AllEnable = true,
            CheckSyntax = true,
            CheckAnnotateType = true,
            CheckTableDuplicateKey = true,
            CheckAssignParamNum = true,
            CheckLocalDefineParamNum = true,
            CheckGotoLable = true,
            CheckFunctionDuplicateParam = true,
            CheckDuplicateIf = true,
            EnableReport = true,
        };
    }

    public string GetVersion()
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(_lualspPath);
            var version = string.IsNullOrEmpty(info.ProductVersion)
                ? info.FileVersion
                : info.ProductVersion;
            if (!string.IsNullOrEmpty(version))
                return $"LuaHelper lualsp.exe {version}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read version from {LualspPath}; falling back to default version.",
                _lualspPath
            );
        }

        return $"LuaHelper lualsp.exe {DefaultVersion}";
    }

    public async Task<string> CreateDefaultConfigAsync(
        string projectPath,
        CancellationToken ct = default
    )
    {
        var config = new
        {
            BaseDir = "./",
            ShowWarnFlag = 1,
            ReferMatchPathFlag = 0,
            IgnoreFileNameVarFlag = 0,
            ProjectFiles = new string[] { },
            IgnoreModules = new[]
            {
                "C_Container",
                "C_UnitAuras",
                "C_Timer",
                "C_AddOns",
                "CreateFrame",
                "GetTime",
                "print",
                "pairs",
                "ipairs",
                "tinsert",
                "tremove",
                "table",
                "string",
                "math",
                "tostring",
                "tonumber",
                "type",
                "error",
                "assert",
                "select",
                "unpack",
                "next",
                "rawget",
                "rawset",
                "setmetatable",
                "getmetatable",
            },
            IgnoreFileVars = new string[] { },
            IgnoreReadFiles = new string[] { },
            IgnoreErrorTypes = new string[] { },
            IgnoreFileOrFloder = new[] { ".vscode/", "Tests/" },
            IgnoreFileErr = new string[] { },
            IgnoreFileErrTypes = new string[] { },
            ProtocolVars = new string[] { },
            ReferFrameFiles = new string[] { },
            PathSeparator = ".",
        };

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var filePath = Path.Combine(projectPath, "luahelper.json");
        await File.WriteAllTextAsync(filePath, json, ct);
        return $"Created luahelper.json at {filePath}";
    }
}
