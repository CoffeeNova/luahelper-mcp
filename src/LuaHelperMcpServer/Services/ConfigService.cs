using System.Text.Json;
using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Services;

public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public LuaHelperConfig GetConfig(string projectPath)
    {
        return new LuaHelperConfig
        {
            ProjectPath = projectPath,
            PluginPath = "",
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
