using System.Text.Json;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Resources;
using LuaHelperMcpServer.Services;
using ModelContextProtocol;
using NSubstitute;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Resources;

public class DiagnosticResourcesTests
{
    private static readonly IFixture Fixture = new Fixture().Customize(
        new AutoNSubstituteCustomization()
    );

    private static string ExistingFilePath => typeof(DiagnosticResourcesTests).Assembly.Location;

    private ILspClient _lspClient = null!;
    private DiagnosticCache _cache = null!;
    private IConfigService _configService = null!;
    private DiagnosticResources _resources = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _lspClient = Fixture.Create<ILspClient>();
        _cache = new DiagnosticCache();
        _configService = Fixture.Create<IConfigService>();
        _resources = new DiagnosticResources(_lspClient, _cache, _configService);
    }

    [Test]
    public async Task GetDiagnostics_FileNotFound_ThrowsMcpException()
    {
        // Act
        var task = _resources.GetDiagnostics(
            "C:\\does\\not\\exist\\test.lua",
            CancellationToken.None
        );

        // Assert
        await Should.ThrowAsync<McpException>(task);
    }

    [Test]
    public async Task GetDiagnostics_Cached_ReturnsCachedJson()
    {
        // Arrange
        var uri = LspClient.PathToUri(ExistingFilePath);
        var cached = new List<LuaDiagnostic>
        {
            new()
            {
                Uri = uri,
                Message = "cached warning",
                Severity = DiagnosticSeverity.Warning,
            },
        };
        _cache.StoreDiagnostics(uri, cached);

        // Act
        var result = await _resources.GetDiagnostics(ExistingFilePath, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        var diag = doc.RootElement.EnumerateArray().Single();
        diag.GetProperty("message").GetString().ShouldBe("cached warning");
        await _lspClient
            .DidNotReceive()
            .EnsureInitializedAsync(
                Arg.Any<string>(),
                Arg.Any<LuaHelperConfig>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Test]
    public async Task GetDiagnostics_Uncached_InitializesAndReturnsJson()
    {
        // Arrange
        var config = new LuaHelperConfig { ProjectPath = "C:\\test" };
        _configService.GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(config);
        _lspClient
            .GetDiagnosticsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                new List<LuaDiagnostic>
                {
                    new() { Message = "live warning", Severity = DiagnosticSeverity.Warning },
                }
            );

        // Act
        var result = await _resources.GetDiagnostics(ExistingFilePath, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.EnumerateArray()
            .Single()
            .GetProperty("message")
            .GetString()
            .ShouldBe("live warning");
        await _lspClient
            .Received(1)
            .EnsureInitializedAsync(
                Arg.Any<string>(),
                Arg.Any<LuaHelperConfig>(),
                Arg.Any<CancellationToken>()
            );
        await _lspClient.Received(1).OpenFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetDiagnostics_Timeout_ReturnsEmptyArray()
    {
        // Arrange — GetDiagnosticsAsync never completes; hardcoded 10s timeout kicks in
        var config = new LuaHelperConfig { ProjectPath = "C:\\test" };
        _configService.GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(config);
        _lspClient
            .GetDiagnosticsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<List<LuaDiagnostic>>().Task);

        // Act
        var result = await _resources.GetDiagnostics(ExistingFilePath, CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task GetDiagnostics_CrashedState_ShutsDownAndReinitializes()
    {
        // Arrange
        var config = new LuaHelperConfig { ProjectPath = "C:\\test" };
        _configService.GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(config);
        _lspClient.State.Returns(LspState.Crashed);
        var firstCall = true;
        _lspClient
            .EnsureInitializedAsync(
                Arg.Any<string>(),
                Arg.Any<LuaHelperConfig>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                if (firstCall)
                {
                    firstCall = false;
                    throw new InvalidOperationException("LSP crashed");
                }
                return Task.CompletedTask;
            });
        _lspClient
            .GetDiagnosticsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<LuaDiagnostic>());

        // Act
        var result = await _resources.GetDiagnostics(ExistingFilePath, CancellationToken.None);

        // Assert
        result.ShouldBe("[]");
        await _lspClient
            .Received(2)
            .EnsureInitializedAsync(
                Arg.Any<string>(),
                Arg.Any<LuaHelperConfig>(),
                Arg.Any<CancellationToken>()
            );
        await _lspClient.Received(1).ShutdownAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetConfig_NoProjectPath_ReturnsDefaultConfig()
    {
        // Arrange
        _lspClient.ProjectPath.Returns((string?)null);

        // Act
        var result = await _resources.GetConfig(CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("allEnable").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("client").GetString().ShouldBe("vsc");
    }

    [Test]
    public async Task GetConfig_WithProjectPath_ReturnsServiceConfig()
    {
        // Arrange
        _lspClient.ProjectPath.Returns("C:\\test");
        _configService
            .GetConfig("C:\\test", Arg.Any<CancellationToken>())
            .Returns(
                new LuaHelperConfig
                {
                    ProjectPath = "C:\\test",
                    AllEnable = false,
                    CheckAnnotateType = false,
                }
            );

        // Act
        var result = await _resources.GetConfig(CancellationToken.None);

        // Assert
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("projectPath").GetString().ShouldBe("C:\\test");
        doc.RootElement.GetProperty("allEnable").GetBoolean().ShouldBeFalse();
    }
}
