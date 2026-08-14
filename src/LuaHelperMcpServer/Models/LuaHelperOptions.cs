namespace LuaHelperMcpServer.Models;

public sealed class LuaHelperOptions
{
    public string LualspPath { get; set; } = string.Empty;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan DiagnosticTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxRestarts { get; set; } = 3;
    public int[] BackoffScheduleSeconds { get; set; } = [2, 4, 8];
    public int IdleTimeoutMinutes { get; set; } = 10;
    public CheckDefaults DefaultChecks { get; set; } = new();
}

public sealed class CheckDefaults
{
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
}
