using System.Text;

namespace LuaHelperMcpServer.Models;

public sealed class DiagnosticCollection
{
    public string ProjectPath { get; init; } = string.Empty;
    public Dictionary<string, List<LuaDiagnostic>> ByFile { get; init; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public int TotalCount => ByFile.Values.Sum(d => d.Count);

    public string ToFormattedString()
    {
        if (TotalCount == 0)
            return $"No warnings found in project {ProjectPath}";

        var sb = new StringBuilder();
        sb.AppendLine(
            $"Project {ProjectPath}: {TotalCount} warning(s) across {ByFile.Count(k => k.Value.Count > 0)} file(s)"
        );

        foreach (var (uri, diagnostics) in ByFile.Where(k => k.Value.Count > 0))
        {
            var filePath = uri.Replace("file:///", string.Empty).Replace("/", "\\");
            sb.AppendLine($"\n--- {filePath} ({diagnostics.Count}) ---");
            foreach (var d in diagnostics)
                sb.AppendLine($"  L{d.StartLine}:{d.StartCharacter} [{d.Severity}] {d.Message}");
        }

        return sb.ToString();
    }
}
