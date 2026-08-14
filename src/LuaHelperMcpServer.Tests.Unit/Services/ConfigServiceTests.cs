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
    public async Task GetConfig_NewFormatPropertyNames_AreMerged()
    {
        // Arrange — modern camelCase format used by users (e.g., ArenaChillPrep)
        const string json = """
            {
              "allEnable": false,
              "checkNoDefine": true,
              "checkLocalNoUse": true,
              "checkAnnotateType": false,
              "checkFloatEq": true,
              "ignoreFileOrDir": [".vscode/", "Tests/", "build/"],
              "ignoreFileOrDirError": ["generated.lua"],
              "requirePathSeparator": "/",
              "enableReport": false
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
        config.CheckNoDefine.ShouldBeTrue();
        config.CheckLocalNoUse.ShouldBeTrue();
        config.CheckAnnotateType.ShouldBeFalse();
        config.CheckFloatEq.ShouldBeTrue();
        config.IgnoreFileOrDir.ShouldBe(new[] { ".vscode/", "Tests/", "build/" });
        config.IgnoreFileOrDirError.ShouldBe(new[] { "generated.lua" });
        config.RequirePathSeparator.ShouldBe("/");
        config.EnableReport.ShouldBeFalse();
    }

    [Test]
    public async Task GetConfig_NewFormatNamesOverrideOldFormat()
    {
        // Arrange — both old and new names present; new should win
        const string json = """
            {
              "ShowWarnFlag": 0,
              "AllEnable": true,
              "IgnoreFileOrFloder": ["old_dir/"],
              "IgnoreFileOrDir": ["new_dir/"],
              "IgnoreFileErr": ["old.lua"],
              "IgnoreFileOrDirError": ["new.lua"],
              "PathSeparator": "\\",
              "RequirePathSeparator": "/"
            }
            """;
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert — new names win
        config.AllEnable.ShouldBeTrue();
        config.IgnoreFileOrDir.ShouldBe(new[] { "new_dir/" });
        config.IgnoreFileOrDirError.ShouldBe(new[] { "new.lua" });
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

    [Test]
    public async Task GetConfig_MalformedJson_LogsAndReturnsDefaults()
    {
        // Arrange
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader
            .ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{ this is not valid json");
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.AllEnable.ShouldBeTrue();
        config.CheckSyntax.ShouldBeTrue();
        config.CheckAnnotateType.ShouldBeTrue();
        config.IgnoreModules.ShouldBeEmpty();
        config.IgnoreFileOrDir.ShouldBe(new[] { ".vscode/", "one11.lua" });
    }

    [TestCase("CheckSyntax", "checkSyntax", false)]
    [TestCase("CheckNoDefine", "checkNoDefine", true)]
    [TestCase("CheckAfterDefine", "checkAfterDefine", true)]
    [TestCase("CheckLocalNoUse", "checkLocalNoUse", true)]
    [TestCase("CheckTableDuplicateKey", "checkTableDuplicateKey", false)]
    [TestCase("CheckReferNoFile", "checkReferNoFile", true)]
    [TestCase("CheckAssignParamNum", "checkAssignParamNum", false)]
    [TestCase("CheckLocalDefineParamNum", "checkLocalDefineParamNum", false)]
    [TestCase("CheckGotoLable", "checkGotoLable", false)]
    [TestCase("CheckFuncParam", "checkFuncParam", true)]
    [TestCase("CheckImportModuleVar", "checkImportModuleVar", true)]
    [TestCase("CheckIfNotVar", "checkIfNotVar", true)]
    [TestCase("CheckFunctionDuplicateParam", "checkFunctionDuplicateParam", false)]
    [TestCase("CheckBinaryExpressionDuplicate", "checkBinaryExpressionDuplicate", true)]
    [TestCase("CheckErrorOrAlwaysTrue", "checkErrorOrAlwaysTrue", true)]
    [TestCase("CheckErrorAndAlwaysFalse", "checkErrorAndAlwaysFalse", true)]
    [TestCase("CheckNoUseAssign", "checkNoUseAssign", true)]
    [TestCase("CheckAnnotateType", "checkAnnotateType", false)]
    [TestCase("CheckDuplicateIf", "checkDuplicateIf", false)]
    [TestCase("CheckSelfAssign", "checkSelfAssign", true)]
    [TestCase("CheckFloatEq", "checkFloatEq", true)]
    public async Task GetConfig_IndividualCheckFlag_IsMerged(
        string propertyName,
        string jsonName,
        bool value
    )
    {
        // Arrange
        var json = $"{{\"{jsonName}\": {(value ? "true" : "false")}}}";
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        var property = typeof(LuaHelperConfig).GetProperty(propertyName);
        property.ShouldNotBeNull();
        property.GetValue(config).ShouldBe(value);
    }

    [Test]
    public async Task GetConfig_EnableReport_IsMerged()
    {
        // Arrange
        const string json = """{ "EnableReport": false }""";
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.EnableReport.ShouldBeFalse();
    }

    [Test]
    public async Task GetConfig_AllEnableBool_WinsOverShowWarnFlag()
    {
        // Arrange — AllEnable=false takes precedence over ShowWarnFlag=1
        const string json = """{ "ShowWarnFlag": 1, "AllEnable": false }""";
        var fileReader = Fixture.Create<IFileReader>();
        fileReader.FileExists(Arg.Any<string>()).Returns(true);
        fileReader.ReadAllTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(json);
        var service = CreateService(fileReader);

        // Act
        var config = await service.GetConfig("C:\\project");

        // Assert
        config.AllEnable.ShouldBeFalse();
    }
}
