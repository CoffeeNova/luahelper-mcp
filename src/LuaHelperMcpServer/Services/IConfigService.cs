using LuaHelperMcpServer.Models;

namespace LuaHelperMcpServer.Services;

public interface IConfigService
{
    LuaHelperConfig GetConfig(string projectPath);
    string GetVersion();
    Task<string> CreateDefaultConfigAsync(string projectPath, CancellationToken ct = default);
}
