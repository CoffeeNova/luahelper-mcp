using LuaHelperMcpServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LuaHelperMcpServer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaHelperServices(
        this IServiceCollection services,
        string lualspPath
    )
    {
        services.AddSingleton<IFileReader, FileReader>();
        services.AddSingleton<IDiagnosticCache, DiagnosticCache>();
        services.AddSingleton<IConfigService>(_ => new ConfigService(lualspPath));
        services.AddSingleton<IProcessManager>(sp => new ProcessManager(
            sp.GetRequiredService<ILogger<ProcessManager>>(),
            lualspPath
        ));
        services.AddSingleton<ILspClient, LspClient>();
        return services;
    }
}
