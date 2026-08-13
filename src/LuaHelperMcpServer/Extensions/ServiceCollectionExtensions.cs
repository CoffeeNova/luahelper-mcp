using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LuaHelperMcpServer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaHelperServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileReader, FileReader>();
        services.AddSingleton<IDiagnosticCache, DiagnosticCache>();
        services.AddSingleton<IConfigService>(sp => new ConfigService(
            sp.GetRequiredService<IOptions<LuaHelperOptions>>(),
            sp.GetRequiredService<ILogger<ConfigService>>(),
            sp.GetRequiredService<IFileReader>()
        ));
        services.AddSingleton<IProcessManager>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LuaHelperOptions>>().Value;
            var backoffSchedule = options
                .BackoffScheduleSeconds?.Select(seconds => TimeSpan.FromSeconds(seconds))
                .ToArray();
            return new ProcessManager(
                sp.GetRequiredService<ILogger<ProcessManager>>(),
                LualspPathResolver.Resolve(options.LualspPath),
                maxRestarts: options.MaxRestarts,
                backoffSchedule: backoffSchedule
            );
        });
        services.AddSingleton<ILspClient, LspClient>();
        return services;
    }
}
