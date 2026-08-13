using AutoFixture;
using AutoFixture.AutoNSubstitute;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class ConfigServiceTests
{
    private static readonly IFixture Fixture = new Fixture().Customize(
        new AutoNSubstituteCustomization()
    );

    private static LuaHelperOptions CreateOptions(string? lualspPath = null) =>
        new() { LualspPath = lualspPath ?? "C:\\tools\\lualsp\\lualsp.exe" };

    private static ConfigService CreateService(
        IFileReader? fileReader = null,
        LuaHelperOptions? options = null
    ) =>
        new(
            Options.Create(options ?? CreateOptions()),
            NullLogger<ConfigService>.Instance,
            fileReader ?? Fixture.Create<IFileReader>()
        );

    [Test]
    public async Task GetConfig_ReturnsDefaultsWithPluginPath()
    {
        // Arrange
        var service = CreateService();

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.ProjectPath.ShouldBe("C:\\project");
        config.PluginPath.ShouldBe("C:\\tools\\lualsp");
        config.AllEnable.ShouldBeTrue();
        config.CheckAnnotateType.ShouldBeTrue();
        config.CheckFloatEq.ShouldBeFalse();
        config.IgnoreModules.ShouldBeEmpty();
    }

    [Test]
    public async Task GetConfig_NoLuahelperJson_UsesDefaults()
    {
        // Arrange
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(false);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.AllEnable.ShouldBeTrue();
        config.CheckSyntax.ShouldBeTrue();
        config.CheckAnnotateType.ShouldBeTrue();
        config.IgnoreModules.ShouldBeEmpty();
        config.IgnoreFileOrDir.ShouldBe(new[] { ".vscode/", "one11.lua" });
        config.RequirePathSeparator.ShouldBe(".");
        await fileReader
            .DidNotReceive()
            .ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetConfig_LuahelperJsonOverridesDefaults()
    {
        // Arrange
        const string json = """
            {
              "ShowWarnFlag": 0,
              "IgnoreModules": ["C_Container", "C_UnitAuras", "CreateFrame"],
              "IgnoreFileOrFloder": ["Tests/", "build/"],
              "IgnoreFileErr": ["generated.lua"],
              "PathSeparator": "/"
            }
            """;
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.AllEnable.ShouldBeFalse();
        config.IgnoreModules.ShouldBe(new[] { "C_Container", "C_UnitAuras", "CreateFrame" });
        config.IgnoreFileOrDir.ShouldBe(new[] { "Tests/", "build/" });
        config.IgnoreFileOrDirError.ShouldBe(new[] { "generated.lua" });
        config.RequirePathSeparator.ShouldBe("/");
        config.CheckAnnotateType.ShouldBeTrue();
    }

    [Test]
    public async Task GetConfig_LuahelperJsonAbsentFields_KeepDefaults()
    {
        // Arrange
        const string json = """{ "ShowWarnFlag": 1 }""";
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.AllEnable.ShouldBeTrue();
        config.IgnoreModules.ShouldBeEmpty();
        config.IgnoreFileOrDir.ShouldBe(new[] { ".vscode/", "one11.lua" });
        config.RequirePathSeparator.ShouldBe(".");
    }

    [Test]
    public async Task GetConfig_LuahelperJsonCamelCaseKeys_AreMerged()
    {
        // Arrange
        const string json = """
            {
              "showWarnFlag": 0,
              "ignoreModules": ["C_Timer"],
              "ignoreFileOrFloder": ["generated/"],
              "ignoreFileErr": ["generated.lua"],
              "pathSeparator": "/"
            }
            """;
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.AllEnable.ShouldBeFalse();
        config.IgnoreModules.ShouldBe(new[] { "C_Timer" });
        config.IgnoreFileOrDir.ShouldBe(new[] { "generated/" });
        config.IgnoreFileOrDirError.ShouldBe(new[] { "generated.lua" });
        config.RequirePathSeparator.ShouldBe("/");
    }

    [Test]
    public void GetVersion_NoFile_FallsBackToDefault()
    {
        // Arrange
        var service = CreateService(options: CreateOptions("C:\\nonexistent\\lualsp.exe"));

        // Act
        var version = service.GetVersion();

        // Assert
        version.ShouldContain("LuaHelper lualsp.exe", Case.Sensitive);
        version.ShouldContain("v0.2.29", Case.Sensitive);
    }
}
