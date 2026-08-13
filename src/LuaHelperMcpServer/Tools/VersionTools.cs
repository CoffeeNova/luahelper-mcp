using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Tools;

[McpServerToolType]
public sealed class VersionTools
{
    [McpServerTool(Name = "get_server_version")]
    [Description("Get the LuaHelper MCP Server version (e.g. \"0.1.0\").")]
    public Task<string> GetServerVersion(CancellationToken ct)
    {
        var version =
            Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0";

        var plusIndex = version.IndexOf('+');
        return Task.FromResult(plusIndex >= 0 ? version[..plusIndex] : version);
    }
}
