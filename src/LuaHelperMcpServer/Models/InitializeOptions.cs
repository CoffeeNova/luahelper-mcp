namespace LuaHelperMcpServer.Models;

public sealed class InitializeOptions
{
    public string Client { get; set; } = "vsc";
    public string PluginPath { get; set; } = "";
    public bool AllEnable { get; set; } = true;
    public bool CheckSyntax { get; set; } = true;
    public bool CheckNoDefine { get; set; } = false;
    public bool CheckAfterDefine { get; set; } = false;
    public bool CheckLocalNoUse { get; set; } = false;
    public bool CheckTableDuplicateKey { get; set; } = true;
    public bool CheckReferNoFile { get; set; } = false;
    public bool CheckAssignParamNum { get; set; } = true;
    public bool CheckLocalDefineParamNum { get; set; } = true;
    public bool CheckGotoLable { get; set; } = true;
    public bool CheckFuncParam { get; set; } = false;
    public bool CheckImportModuleVar { get; set; } = false;
    public bool CheckIfNotVar { get; set; } = false;
    public bool CheckFunctionDuplicateParam { get; set; } = true;
    public bool CheckBinaryExpressionDuplicate { get; set; } = false;
    public bool CheckErrorOrAlwaysTrue { get; set; } = false;
    public bool CheckErrorAndAlwaysFalse { get; set; } = false;
    public bool CheckNoUseAssign { get; set; } = false;
    public bool CheckAnnotateType { get; set; } = true;
    public bool CheckDuplicateIf { get; set; } = true;
    public bool CheckSelfAssign { get; set; } = false;
    public bool CheckFloatEq { get; set; } = false;
    public List<string> IgnoreFileOrDir { get; set; } = [];
    public List<string> IgnoreFileOrDirError { get; set; } = [];
    public string RequirePathSeparator { get; set; } = ".";
    public bool EnableReport { get; set; } = true;
}
