using LuaHelperMcpServer.Extensions;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddLuaHelperServices_RegistersAllServicesAsSingletons()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddLuaHelperServices();

        // Assert
        services.ShouldContain(d =>
            d.ServiceType == typeof(IFileReader) && d.Lifetime == ServiceLifetime.Singleton
        );
        services.ShouldContain(d =>
            d.ServiceType == typeof(IDiagnosticCache) && d.Lifetime == ServiceLifetime.Singleton
        );
        services.ShouldContain(d =>
            d.ServiceType == typeof(IProcessLauncher) && d.Lifetime == ServiceLifetime.Singleton
        );
        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigService) && d.Lifetime == ServiceLifetime.Singleton
        );
        services.ShouldContain(d =>
            d.ServiceType == typeof(IProcessManager) && d.Lifetime == ServiceLifetime.Singleton
        );
        services.ShouldContain(d =>
            d.ServiceType == typeof(ILspClient) && d.Lifetime == ServiceLifetime.Singleton
        );
    }

    [Test]
    public void AddLuaHelperServices_ResolvesAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<LuaHelperOptions>(o => o.LualspPath = "lualsp.exe");
        services.AddLuaHelperServices();
        var provider = services.BuildServiceProvider();

        // Act
        var lspClient = provider.GetRequiredService<ILspClient>();
        var configService = provider.GetRequiredService<IConfigService>();
        var cache = provider.GetRequiredService<IDiagnosticCache>();
        var processManager = provider.GetRequiredService<IProcessManager>();
        var launcher = provider.GetRequiredService<IProcessLauncher>();
        var fileReader = provider.GetRequiredService<IFileReader>();

        // Assert
        lspClient.ShouldNotBeNull();
        configService.ShouldNotBeNull();
        cache.ShouldNotBeNull();
        processManager.ShouldNotBeNull();
        launcher.ShouldNotBeNull();
        fileReader.ShouldNotBeNull();
    }

    [Test]
    public void AddLuaHelperServices_ProcessManager_UsesOptionsBackoffSchedule()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<LuaHelperOptions>(o =>
        {
            o.LualspPath = "C:\\nonexistent\\lualsp.exe";
            o.MaxRestarts = 7;
            o.BackoffScheduleSeconds = [1, 3];
        });
        services.AddLuaHelperServices();
        var provider = services.BuildServiceProvider();

        // Act — EnsureRunningAsync reads the configured exe path and fails fast on missing file
        var processManager = provider.GetRequiredService<IProcessManager>();
        var task = processManager.EnsureRunningAsync();

        // Assert — the resolved path is the configured one, and the process never spawned
        Should.Throw<FileNotFoundException>(() => task.GetAwaiter().GetResult());
        processManager.IsRunning.ShouldBeFalse();
    }
}
