namespace LuaHelperMcpServer.Models;

public sealed class LuaHelperConfig
{
    public string ProjectPath { get; set; } = string.Empty;
    public string Client { get; set; } = "vsc";
    public string PluginPath { get; set; } = string.Empty;
    public bool AllEnable { get; set; } = true;
    public bool CheckSyntax { get; set; } = true;
    public bool CheckNoDefine { get; set; }
    public bool CheckAfterDefine { get; set; }
    public bool CheckLocalNoUse { get; set; }
    public bool CheckTableDuplicateKey { get; set; } = true;
    public bool CheckReferNoFile { get; set; }
    public bool CheckAssignParamNum { get; set; } = true;
    public bool CheckLocalDefineParamNum { get; set; } = true;
    public bool CheckGotoLable { get; set; } = true;
    public bool CheckFuncParam { get; set; }
    public bool CheckImportModuleVar { get; set; }
    public bool CheckIfNotVar { get; set; }
    public bool CheckFunctionDuplicateParam { get; set; } = true;
    public bool CheckBinaryExpressionDuplicate { get; set; }
    public bool CheckErrorOrAlwaysTrue { get; set; }
    public bool CheckErrorAndAlwaysFalse { get; set; }
    public bool CheckNoUseAssign { get; set; }
    public bool CheckAnnotateType { get; set; } = true;
    public bool CheckDuplicateIf { get; set; } = true;
    public bool CheckSelfAssign { get; set; }
    public bool CheckFloatEq { get; set; }
    public List<string> IgnoreModules { get; set; } = [];
    public List<string> IgnoreFileOrDir { get; set; } = [".vscode/", "one11.lua"];
    public List<string> IgnoreFileOrDirError { get; set; } = [".vscode/", "one11.lua"];
    public string RequirePathSeparator { get; set; } = ".";
    public bool EnableReport { get; set; } = true;
}
