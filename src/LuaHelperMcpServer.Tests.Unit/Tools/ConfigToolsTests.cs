using AutoFixture;
using AutoFixture.AutoNSubstitute;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tools;
using NSubstitute;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Tools;

public class ConfigToolsTests
{
    private static readonly IFixture Fixture = new Fixture().Customize(
        new AutoNSubstituteCustomization()
    );

    private IConfigService _configService = null!;
    private ConfigTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _configService = Fixture.Create<IConfigService>();
        _tools = new ConfigTools(_configService);
    }

    [Test]
    public async Task GetLuahelperConfig_InvalidDirectory_ReturnsError()
    {
        // Act
        var result = await _tools.GetLuahelperConfig(
            "C:\\does\\not\\exist",
            CancellationToken.None
        );

        // Assert
        result.ShouldContain("Error: Directory not found", Case.Sensitive);
    }

    [Test]
    public async Task GetLuahelperConfig_ValidDirectory_ReturnsConfigJson()
    {
        // Arrange
        var config = new LuaHelperConfig { ProjectPath = AppContext.BaseDirectory };
        _configService
            .GetConfig(AppContext.BaseDirectory, Arg.Any<CancellationToken>())
            .Returns(config);

        // Act
        var result = await _tools.GetLuahelperConfig(
            AppContext.BaseDirectory,
            CancellationToken.None
        );

        // Assert
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        var projectPath = doc.RootElement.GetProperty("projectPath").GetString();
        projectPath.ShouldBe(AppContext.BaseDirectory);
    }

    [Test]
    public async Task CreateLuahelperJson_InvalidDirectory_ReturnsError()
    {
        // Act
        var result = await _tools.CreateLuahelperJson(
            "C:\\does\\not\\exist",
            CancellationToken.None
        );

        // Assert
        result.ShouldContain("Error: Directory not found", Case.Sensitive);
        await _configService
            .DidNotReceive()
            .CreateDefaultConfig(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
