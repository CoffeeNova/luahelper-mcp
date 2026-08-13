using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Services;

public interface IConfigService
{
    Task<LuaHelperConfig> GetConfig(string projectPath, CancellationToken ct = default);
    string GetVersion();
    Task<string> CreateDefaultConfig(string projectPath, CancellationToken ct = default);
}
