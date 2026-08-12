namespace LuaHelperMcpServer.Models;

public sealed class SupportedCheck
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool DefaultOn { get; init; }
}
