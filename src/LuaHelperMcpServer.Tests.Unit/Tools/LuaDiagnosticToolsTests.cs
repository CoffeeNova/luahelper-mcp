using System.Text.Json;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tools;
using NSubstitute;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Tools;

public class LuaDiagnosticToolsTests
{
    private static readonly IFixture Fixture = new Fixture().Customize(
        new AutoNSubstituteCustomization()
    );

    private ILspClient _lspClient = null!;
    private IDiagnosticCache _cache = null!;
    private IConfigService _configService = null!;
    private LuaDiagnosticTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _lspClient = Fixture.Create<ILspClient>();
        _cache = Fixture.Create<IDiagnosticCache>();
        _configService = Fixture.Create<IConfigService>();
        _tools = new LuaDiagnosticTools(_lspClient, _cache, _configService);
    }

    [Test]
    public async Task GetSupportedChecks_ReturnsAll21Checks()
    {
        // Act
        var result = await _tools.GetSupportedChecks(CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        var checks = doc.RootElement.EnumerateArray().ToList();
        checks.Count.ShouldBe(21);
        checks[0].GetProperty("name").GetString().ShouldBe("Syntax errors");
        checks[0].GetProperty("defaultOn").GetBoolean().ShouldBeTrue();
        checks[^1].GetProperty("name").GetString().ShouldBe("Float equality");
        checks[^1].GetProperty("defaultOn").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task GetLuahelperVersion_ReturnsConfiguredVersion()
    {
        // Arrange
        _configService.GetVersion().Returns("LuaHelper lualsp.exe v0.2.29");

        // Act
        var result = await _tools.GetLuahelperVersion(CancellationToken.None);

        // Assert
        result.ShouldBe("LuaHelper lualsp.exe v0.2.29");
    }
}
