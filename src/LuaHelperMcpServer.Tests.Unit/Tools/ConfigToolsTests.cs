using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tools;
using Moq;

namespace LuaHelperMcpServer.Tests.Unit.Tools;

public class ConfigToolsTests
{
    private Mock<IConfigService> _configServiceMock = null!;
    private ConfigTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _configServiceMock = new Mock<IConfigService>();
        _tools = new ConfigTools(_configServiceMock.Object);
    }

    [Test]
    public async Task GetLuahelperConfig_InvalidDirectory_ReturnsError()
    {
        var result = await _tools.GetLuahelperConfig(
            "C:\\does\\not\\exist",
            CancellationToken.None
        );

        Assert.That(result, Does.Contain("Error: Directory not found"));
    }

    [Test]
    public async Task GetLuahelperConfig_ValidDirectory_ReturnsConfigJson()
    {
        var config = new LuaHelperConfig { ProjectPath = AppContext.BaseDirectory };
        _configServiceMock
            .Setup(c => c.GetConfig(AppContext.BaseDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _tools.GetLuahelperConfig(
            AppContext.BaseDirectory,
            CancellationToken.None
        );

        using var doc = System.Text.Json.JsonDocument.Parse(result);
        var projectPath = doc.RootElement.GetProperty("projectPath").GetString();

        Assert.That(projectPath, Is.EqualTo(AppContext.BaseDirectory));
    }

    [Test]
    public async Task CreateLuahelperJson_InvalidDirectory_ReturnsError()
    {
        var result = await _tools.CreateLuahelperJson(
            "C:\\does\\not\\exist",
            CancellationToken.None
        );

        Assert.That(result, Does.Contain("Error: Directory not found"));
        _configServiceMock.Verify(
            c => c.CreateDefaultConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
