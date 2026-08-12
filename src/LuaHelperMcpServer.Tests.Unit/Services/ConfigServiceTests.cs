using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class ConfigServiceTests
{
    [Test]
    public void GetConfig_ReturnsDefaultsWithPluginPath()
    {
        var service = new ConfigService(
            "C:\\tools\\lualsp\\lualsp.exe",
            NullLogger<ConfigService>.Instance
        );

        var config = service.GetConfig("C:\\project");

        Assert.That(config.ProjectPath, Is.EqualTo("C:\\project"));
        Assert.That(config.PluginPath, Is.EqualTo("C:\\tools\\lualsp"));
        Assert.That(config.AllEnable, Is.True);
        Assert.That(config.CheckAnnotateType, Is.True);
        Assert.That(config.CheckFloatEq, Is.False);
    }

    [Test]
    public void GetVersion_NoFile_FallsBackToDefault()
    {
        var service = new ConfigService(
            "C:\\nonexistent\\lualsp.exe",
            NullLogger<ConfigService>.Instance
        );

        var version = service.GetVersion();

        Assert.That(version, Does.Contain("LuaHelper lualsp.exe"));
        Assert.That(version, Does.Contain("v0.2.29"));
    }
}
