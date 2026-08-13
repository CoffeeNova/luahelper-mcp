using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class ConfigServiceTests
{
    private static LuaHelperOptions CreateOptions(string? lualspPath = null) =>
        new() { LualspPath = lualspPath ?? "C:\\tools\\lualsp\\lualsp.exe" };

    private static ConfigService CreateService(
        IFileReader? fileReader = null,
        LuaHelperOptions? options = null
    ) =>
        new(
            Options.Create(options ?? CreateOptions()),
            NullLogger<ConfigService>.Instance,
            fileReader ?? Mock.Of<IFileReader>()
        );

    [Test]
    public async Task GetConfig_ReturnsDefaultsWithPluginPath()
    {
        var service = CreateService();

        var config = await service.GetConfig("C:\\project");

        Assert.That(config.ProjectPath, Is.EqualTo("C:\\project"));
        Assert.That(config.PluginPath, Is.EqualTo("C:\\tools\\lualsp"));
        Assert.That(config.AllEnable, Is.True);
        Assert.That(config.CheckAnnotateType, Is.True);
        Assert.That(config.CheckFloatEq, Is.False);
        Assert.That(config.IgnoreModules, Is.Empty);
    }

    [Test]
    public async Task GetConfig_NoLuahelperJson_UsesDefaults()
    {
        var fileReaderMock = new Mock<IFileReader>();
        fileReaderMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        var service = CreateService(fileReaderMock.Object);

        var config = await service.GetConfig("C:\\project");

        Assert.That(config.AllEnable, Is.True);
        Assert.That(config.CheckSyntax, Is.True);
        Assert.That(config.CheckAnnotateType, Is.True);
        Assert.That(config.IgnoreModules, Is.Empty);
        Assert.That(config.IgnoreFileOrDir, Is.EqualTo(new[] { ".vscode/", "one11.lua" }));
        Assert.That(config.RequirePathSeparator, Is.EqualTo("."));
        fileReaderMock.Verify(
            f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task GetConfig_LuahelperJsonOverridesDefaults()
    {
        const string json = """
            {
              "ShowWarnFlag": 0,
              "IgnoreModules": ["C_Container", "C_UnitAuras", "CreateFrame"],
              "IgnoreFileOrFloder": ["Tests/", "build/"],
              "IgnoreFileErr": ["generated.lua"],
              "PathSeparator": "/"
            }
            """;
        var fileReaderMock = new Mock<IFileReader>();
        fileReaderMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        fileReaderMock
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        var service = CreateService(fileReaderMock.Object);

        var config = await service.GetConfig("C:\\project");

        Assert.That(config.AllEnable, Is.False);
        Assert.That(
            config.IgnoreModules,
            Is.EqualTo(new[] { "C_Container", "C_UnitAuras", "CreateFrame" })
        );
        Assert.That(config.IgnoreFileOrDir, Is.EqualTo(new[] { "Tests/", "build/" }));
        Assert.That(config.IgnoreFileOrDirError, Is.EqualTo(new[] { "generated.lua" }));
        Assert.That(config.RequirePathSeparator, Is.EqualTo("/"));
        Assert.That(config.CheckAnnotateType, Is.True);
    }

    [Test]
    public async Task GetConfig_LuahelperJsonAbsentFields_KeepDefaults()
    {
        const string json = """{ "ShowWarnFlag": 1 }""";
        var fileReaderMock = new Mock<IFileReader>();
        fileReaderMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        fileReaderMock
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        var service = CreateService(fileReaderMock.Object);

        var config = await service.GetConfig("C:\\project");

        Assert.That(config.AllEnable, Is.True);
        Assert.That(config.IgnoreModules, Is.Empty);
        Assert.That(config.IgnoreFileOrDir, Is.EqualTo(new[] { ".vscode/", "one11.lua" }));
        Assert.That(config.RequirePathSeparator, Is.EqualTo("."));
    }

    [Test]
    public async Task GetConfig_LuahelperJsonCamelCaseKeys_AreMerged()
    {
        const string json = """
            {
              "showWarnFlag": 0,
              "ignoreModules": ["C_Timer"],
              "ignoreFileOrFloder": ["generated/"],
              "ignoreFileErr": ["generated.lua"],
              "pathSeparator": "/"
            }
            """;
        var fileReaderMock = new Mock<IFileReader>();
        fileReaderMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        fileReaderMock
            .Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        var service = CreateService(fileReaderMock.Object);

        var config = await service.GetConfig("C:\\project");

        Assert.That(config.AllEnable, Is.False);
        Assert.That(config.IgnoreModules, Is.EqualTo(new[] { "C_Timer" }));
        Assert.That(config.IgnoreFileOrDir, Is.EqualTo(new[] { "generated/" }));
        Assert.That(config.IgnoreFileOrDirError, Is.EqualTo(new[] { "generated.lua" }));
        Assert.That(config.RequirePathSeparator, Is.EqualTo("/"));
    }

    [Test]
    public void GetVersion_NoFile_FallsBackToDefault()
    {
        var service = CreateService(options: CreateOptions("C:\\nonexistent\\lualsp.exe"));

        var version = service.GetVersion();

        Assert.That(version, Does.Contain("LuaHelper lualsp.exe"));
        Assert.That(version, Does.Contain("v0.2.29"));
    }
}
