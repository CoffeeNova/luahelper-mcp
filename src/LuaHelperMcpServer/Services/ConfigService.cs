using System.Diagnostics;
using System.Text.Json;
using LuaHelperMcpServer.Extensions;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LuaHelperMcpServer.Services;

public sealed class ConfigService : IConfigService
{
    private const string DefaultVersion = "v0.2.29";
    private const string LuahelperJsonFileName = "luahelper.json";

    private readonly IOptions<LuaHelperOptions> _options;
    private readonly IFileReader _fileReader;
    private readonly ILogger<ConfigService> _logger;

    public ConfigService(
        IOptions<LuaHelperOptions> options,
        ILogger<ConfigService> logger,
        IFileReader fileReader
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
    }

    public async Task<LuaHelperConfig> GetConfig(string projectPath, CancellationToken ct = default)
    {
        var config = BuildDefaultConfig(projectPath);

        var jsonPath = Path.Combine(projectPath, LuahelperJsonFileName);
        if (!_fileReader.FileExists(jsonPath))
            return config;

        try
        {
            var json = await _fileReader.ReadAllTextAsync(jsonPath, ct);
            MergeLuahelperJson(config, json);
            _logger.LogInformation("Loaded project configuration from {Path}", jsonPath);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse {Path}; using default configuration", jsonPath);
        }

        return config;
    }

    public string GetVersion()
    {
        var lualspPath = LualspPathResolver.Resolve(_options.Value.LualspPath);
        try
        {
            var info = FileVersionInfo.GetVersionInfo(lualspPath);
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
                lualspPath
            );
        }

        return $"LuaHelper lualsp.exe {DefaultVersion}";
    }

    public async Task<string> CreateDefaultConfig(
        string projectPath,
        CancellationToken ct = default
    )
    {
        var config = new LuahelperJsonTemplate();

        var json = JsonSerializer.Serialize(config, LspJson.Indented.LuahelperJsonTemplate);
        var filePath = Path.Combine(projectPath, LuahelperJsonFileName);
        await File.WriteAllTextAsync(filePath, json, ct);
        return $"Created luahelper.json at {filePath}";
    }

    private LuaHelperConfig BuildDefaultConfig(string projectPath)
    {
        var checks = _options.Value.DefaultChecks ?? new CheckDefaults();
        return new LuaHelperConfig
        {
            ProjectPath = projectPath,
            PluginPath =
                Path.GetDirectoryName(LualspPathResolver.Resolve(_options.Value.LualspPath))
                ?? string.Empty,
            AllEnable = checks.AllEnable,
            CheckSyntax = checks.CheckSyntax,
            CheckNoDefine = checks.CheckNoDefine,
            CheckAfterDefine = checks.CheckAfterDefine,
            CheckLocalNoUse = checks.CheckLocalNoUse,
            CheckTableDuplicateKey = checks.CheckTableDuplicateKey,
            CheckReferNoFile = checks.CheckReferNoFile,
            CheckAssignParamNum = checks.CheckAssignParamNum,
            CheckLocalDefineParamNum = checks.CheckLocalDefineParamNum,
            CheckGotoLable = checks.CheckGotoLable,
            CheckFuncParam = checks.CheckFuncParam,
            CheckImportModuleVar = checks.CheckImportModuleVar,
            CheckIfNotVar = checks.CheckIfNotVar,
            CheckFunctionDuplicateParam = checks.CheckFunctionDuplicateParam,
            CheckBinaryExpressionDuplicate = checks.CheckBinaryExpressionDuplicate,
            CheckErrorOrAlwaysTrue = checks.CheckErrorOrAlwaysTrue,
            CheckErrorAndAlwaysFalse = checks.CheckErrorAndAlwaysFalse,
            CheckNoUseAssign = checks.CheckNoUseAssign,
            CheckAnnotateType = checks.CheckAnnotateType,
            CheckDuplicateIf = checks.CheckDuplicateIf,
            CheckSelfAssign = checks.CheckSelfAssign,
            CheckFloatEq = checks.CheckFloatEq,
        };
    }

    private static void MergeLuahelperJson(LuaHelperConfig config, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (TryGetProperty(root, "ShowWarnFlag", out var showWarnFlag))
            config.AllEnable = showWarnFlag.GetInt32() == 1;

        if (TryGetStringArray(root, "IgnoreModules", out var ignoreModules))
            config.IgnoreModules = ignoreModules;

        if (TryGetStringArray(root, "IgnoreFileOrFloder", out var ignoreFileOrFloder))
            config.IgnoreFileOrDir = ignoreFileOrFloder;

        if (TryGetStringArray(root, "IgnoreFileErr", out var ignoreFileErr))
            config.IgnoreFileOrDirError = ignoreFileErr;

        if (
            TryGetProperty(root, "PathSeparator", out var pathSeparator)
            && pathSeparator.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(pathSeparator.GetString())
        )
            config.RequirePathSeparator = pathSeparator.GetString()!;
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
            return true;

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetStringArray(
        JsonElement root,
        string propertyName,
        out List<string> values
    )
    {
        values = [];
        if (
            !TryGetProperty(root, propertyName, out var element)
            || element.ValueKind != JsonValueKind.Array
        )
            return false;

        values = element
            .EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();
        return true;
    }
}
