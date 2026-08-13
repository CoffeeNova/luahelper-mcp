using AutoFixture;
using AutoFixture.AutoNSubstitute;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class LspClientTests
{
    private static readonly IFixture Fixture = new Fixture().Customize(
        new AutoNSubstituteCustomization()
    );

    private FakeLspServer _fakeServer = null!;
    private MockProcessManager _processManager = null!;
    private DiagnosticCache _cache = null!;
    private LspClient _client = null!;
    private IFileReader _fileReader = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _fakeServer = new FakeLspServer();
        _processManager = new MockProcessManager(_fakeServer);
        _cache = new DiagnosticCache();
        _fileReader = Fixture.Create<IFileReader>();
        _client = new LspClient(
            _processManager,
            _cache,
            NullLogger<LspClient>.Instance,
            _fileReader
        );
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _fakeServer.Dispose();
    }

    [Test]
    public async Task EnsureInitializedAsync_SendsInitialize_ReceivesCapabilities()
    {
        // Arrange
        _fakeServer.Start();

        // Act
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Assert
        _client.State.ShouldBe(LspState.Ready);
    }

    [Test]
    public async Task EnsureInitializedAsync_SetsProjectPath()
    {
        // Arrange
        _fakeServer.Start();

        // Act
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Assert
        _client.ProjectPath.ShouldBe("C:\\test");
    }

    [Test]
    public async Task OpenFileAsync_SendsDidOpen()
    {
        // Arrange
        var testFile = "C:\\test\\test.lua";
        _fileReader.FileExists(testFile).Returns(true);
        _fileReader.ReadAllTextAsync(testFile, Arg.Any<CancellationToken>()).Returns("local x = 1");
        _fakeServer.Start();

        // Act
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );
        await _client.OpenFileAsync(testFile);

        // Assert
        var uri = LspClient.PathToUri(testFile);
        _cache.GetFileContent(uri).ShouldBe("local x = 1");
    }

    [Test]
    public async Task GetDiagnosticsAsync_ReceivesPublishDiagnostics()
    {
        // Arrange
        var testFile = "C:\\test\\test.lua";
        _fileReader.FileExists(testFile).Returns(true);
        _fileReader.ReadAllTextAsync(testFile, Arg.Any<CancellationToken>()).Returns("local x = 1");
        _fakeServer.Start();

        // Act
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );
        await _client.OpenFileAsync(testFile);
        var diagnostics = await _client.GetDiagnosticsAsync(testFile);

        // Assert
        diagnostics.ShouldNotBeEmpty();
        diagnostics[0].Message.ShouldContain("Test warning", Case.Sensitive);
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostics[0].WarningType.ShouldBe(1);
        diagnostics[0].StartLine.ShouldBe(0);
        diagnostics[0].StartCharacter.ShouldBe(0);
    }

    [Test]
    public async Task GetAllDiagnostics_ReturnsAllDiagnostics()
    {
        // Arrange
        var testFile = "C:\\test\\test.lua";
        _fileReader.FileExists(testFile).Returns(true);
        _fileReader.ReadAllTextAsync(testFile, Arg.Any<CancellationToken>()).Returns("local x = 1");
        _fakeServer.Start();

        // Act
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );
        await _client.OpenFileAsync(testFile);
        await _client.GetDiagnosticsAsync(testFile);
        var all = _client.GetAllDiagnostics();

        // Assert
        all.ShouldNotBeEmpty();
        all.Values.Sum(d => d.Count).ShouldBe(1);
    }

    [Test]
    public async Task ShutdownAsync_ChangesStateToStopped()
    {
        // Arrange
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Act
        await _client.ShutdownAsync();

        // Assert
        _client.State.ShouldBe(LspState.Stopped);
    }
}
