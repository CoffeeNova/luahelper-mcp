namespace LuaHelperMcpServer.Models;

public sealed class LuaDiagnostic
{
    public string Uri { get; init; } = "";
    public int StartLine { get; init; }
    public int StartCharacter { get; init; }
    public int EndLine { get; init; }
    public int EndCharacter { get; init; }
    public DiagnosticSeverity Severity { get; init; }
    public int WarningType { get; init; }
    public string Message { get; init; } = "";
    public string? Source { get; init; }

    public string ToFormattedString() => $"L{StartLine}:{StartCharacter} [{Severity}] {Message}";
}
