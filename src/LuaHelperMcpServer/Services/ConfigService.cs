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

        if (TryGetBoolProperty(root, "AllEnable", out var allEnable))
            config.AllEnable = allEnable;
        else if (TryGetProperty(root, "ShowWarnFlag", out var showWarnFlag))
            config.AllEnable = showWarnFlag.GetInt32() == 1;

        TryMergeCheckFlags(config, root);

        if (TryGetStringArray(root, "IgnoreModules", out var ignoreModules))
            config.IgnoreModules = ignoreModules;

        if (
            !TryGetStringArray(root, "IgnoreFileOrDir", out var ignoreFileOrDir)
            && !TryGetStringArray(root, "IgnoreFileOrFloder", out ignoreFileOrDir)
        )
            ignoreFileOrDir = null;
        if (ignoreFileOrDir != null)
            config.IgnoreFileOrDir = ignoreFileOrDir;

        if (
            !TryGetStringArray(root, "IgnoreFileOrDirError", out var ignoreFileOrDirError)
            && !TryGetStringArray(root, "IgnoreFileErr", out ignoreFileOrDirError)
        )
            ignoreFileOrDirError = null;
        if (ignoreFileOrDirError != null)
            config.IgnoreFileOrDirError = ignoreFileOrDirError;

        if (!TryGetStringProperty(root, "RequirePathSeparator", out var requirePathSeparator))
            TryGetStringProperty(root, "PathSeparator", out requirePathSeparator);
        if (requirePathSeparator != null)
            config.RequirePathSeparator = requirePathSeparator;

        if (TryGetBoolProperty(root, "EnableReport", out var enableReport))
            config.EnableReport = enableReport;
    }

    private static void TryMergeCheckFlags(LuaHelperConfig config, JsonElement root)
    {
        if (TryGetBoolProperty(root, "CheckSyntax", out var v))
            config.CheckSyntax = v;
        if (TryGetBoolProperty(root, "CheckNoDefine", out v))
            config.CheckNoDefine = v;
        if (TryGetBoolProperty(root, "CheckAfterDefine", out v))
            config.CheckAfterDefine = v;
        if (TryGetBoolProperty(root, "CheckLocalNoUse", out v))
            config.CheckLocalNoUse = v;
        if (TryGetBoolProperty(root, "CheckTableDuplicateKey", out v))
            config.CheckTableDuplicateKey = v;
        if (TryGetBoolProperty(root, "CheckReferNoFile", out v))
            config.CheckReferNoFile = v;
        if (TryGetBoolProperty(root, "CheckAssignParamNum", out v))
            config.CheckAssignParamNum = v;
        if (TryGetBoolProperty(root, "CheckLocalDefineParamNum", out v))
            config.CheckLocalDefineParamNum = v;
        if (TryGetBoolProperty(root, "CheckGotoLable", out v))
            config.CheckGotoLable = v;
        if (TryGetBoolProperty(root, "CheckFuncParam", out v))
            config.CheckFuncParam = v;
        if (TryGetBoolProperty(root, "CheckImportModuleVar", out v))
            config.CheckImportModuleVar = v;
        if (TryGetBoolProperty(root, "CheckIfNotVar", out v))
            config.CheckIfNotVar = v;
        if (TryGetBoolProperty(root, "CheckFunctionDuplicateParam", out v))
            config.CheckFunctionDuplicateParam = v;
        if (TryGetBoolProperty(root, "CheckBinaryExpressionDuplicate", out v))
            config.CheckBinaryExpressionDuplicate = v;
        if (TryGetBoolProperty(root, "CheckErrorOrAlwaysTrue", out v))
            config.CheckErrorOrAlwaysTrue = v;
        if (TryGetBoolProperty(root, "CheckErrorAndAlwaysFalse", out v))
            config.CheckErrorAndAlwaysFalse = v;
        if (TryGetBoolProperty(root, "CheckNoUseAssign", out v))
            config.CheckNoUseAssign = v;
        if (TryGetBoolProperty(root, "CheckAnnotateType", out v))
            config.CheckAnnotateType = v;
        if (TryGetBoolProperty(root, "CheckDuplicateIf", out v))
            config.CheckDuplicateIf = v;
        if (TryGetBoolProperty(root, "CheckSelfAssign", out v))
            config.CheckSelfAssign = v;
        if (TryGetBoolProperty(root, "CheckFloatEq", out v))
            config.CheckFloatEq = v;
    }

    private static bool TryGetBoolProperty(JsonElement root, string propertyName, out bool value)
    {
        if (TryGetProperty(root, propertyName, out var element))
        {
            if (element.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            if (element.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
        }
        value = false;
        return false;
    }

    private static bool TryGetStringProperty(
        JsonElement root,
        string propertyName,
        out string? value
    )
    {
        if (
            TryGetProperty(root, propertyName, out var element)
            && element.ValueKind == JsonValueKind.String
        )
        {
            value = element.GetString();
            return !string.IsNullOrEmpty(value);
        }
        value = null;
        return false;
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
