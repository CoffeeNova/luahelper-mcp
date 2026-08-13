using System.Text.Json;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tools;
using Moq;

namespace LuaHelperMcpServer.Tests.Unit.Tools;

public class LuaDiagnosticToolsTests
{
    private Mock<ILspClient> _lspClientMock = null!;
    private Mock<IDiagnosticCache> _cacheMock = null!;
    private Mock<IConfigService> _configServiceMock = null!;
    private LuaDiagnosticTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _lspClientMock = new Mock<ILspClient>();
        _cacheMock = new Mock<IDiagnosticCache>();
        _configServiceMock = new Mock<IConfigService>();
        _tools = new LuaDiagnosticTools(
            _lspClientMock.Object,
            _cacheMock.Object,
            _configServiceMock.Object
        );
    }

    [Test]
    public async Task GetSupportedChecks_ReturnsAll21Checks()
    {
        var result = await _tools.GetSupportedChecks(CancellationToken.None);

        using var doc = JsonDocument.Parse(result);
        var checks = doc.RootElement.EnumerateArray().ToList();

        Assert.That(checks, Has.Count.EqualTo(21));
        Assert.That(checks[0].GetProperty("name").GetString(), Is.EqualTo("Syntax errors"));
        Assert.That(checks[0].GetProperty("defaultOn").GetBoolean(), Is.True);
        Assert.That(checks[^1].GetProperty("name").GetString(), Is.EqualTo("Float equality"));
        Assert.That(checks[^1].GetProperty("defaultOn").GetBoolean(), Is.False);
    }

    [Test]
    public async Task GetLuahelperVersion_ReturnsConfiguredVersion()
    {
        _configServiceMock.Setup(c => c.GetVersion()).Returns("LuaHelper lualsp.exe v0.2.29");

        var result = await _tools.GetLuahelperVersion(CancellationToken.None);

        Assert.That(result, Is.EqualTo("LuaHelper lualsp.exe v0.2.29"));
    }
}
