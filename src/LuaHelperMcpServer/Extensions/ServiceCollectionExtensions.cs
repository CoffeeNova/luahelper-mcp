using LuaHelperMcpServer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LuaHelperMcpServer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaHelperServices(this IServiceCollection services)
    {
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IDiagnosticCache, DiagnosticCache>();
        services.AddSingleton<ILspClient, LspClient>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IFileReader, FileReader>();
        return services;
    }
}
