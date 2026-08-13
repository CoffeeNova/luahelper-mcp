namespace LuaHelperMcpServer.Models;

public sealed class LuahelperJsonTemplate
{
    public string BaseDir { get; set; } = "./";
    public int ShowWarnFlag { get; set; } = 1;
    public int ReferMatchPathFlag { get; set; } = 0;
    public int IgnoreFileNameVarFlag { get; set; } = 0;
    public string[] ProjectFiles { get; set; } = [];
    public string[] IgnoreModules { get; set; } =
        [
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
        ];
    public string[] IgnoreFileVars { get; set; } = [];
    public string[] IgnoreReadFiles { get; set; } = [];
    public string[] IgnoreErrorTypes { get; set; } = [];
    public string[] IgnoreFileOrFloder { get; set; } = [".vscode/", "Tests/"];
    public string[] IgnoreFileErr { get; set; } = [];
    public string[] IgnoreFileErrTypes { get; set; } = [];
    public string[] ProtocolVars { get; set; } = [];
    public string[] ReferFrameFiles { get; set; } = [];
    public string PathSeparator { get; set; } = ".";
}
