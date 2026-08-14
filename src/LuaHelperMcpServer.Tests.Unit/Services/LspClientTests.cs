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

    [Test]
    public async Task OpenFileAsync_NotReady_ThrowsInvalidOperation()
    {
        // Act & Assert — client never initialized
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _client.OpenFileAsync("C:\\test\\test.lua")
        );
    }

    [Test]
    public async Task OpenFileAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var missingFile = "C:\\test\\missing.lua";
        _fileReader.FileExists(missingFile).Returns(false);
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Act
        var task = _client.OpenFileAsync(missingFile);

        // Assert
        await Should.ThrowAsync<FileNotFoundException>(task);
    }

    [Test]
    public async Task GetDiagnosticsAsync_AlreadyCached_ReturnsCached()
    {
        // Arrange
        var uri = LspClient.PathToUri("C:\\test\\test.lua");
        var cached = new List<LuaDiagnostic>
        {
            new()
            {
                Uri = uri,
                Message = "cached",
                Severity = DiagnosticSeverity.Warning,
            },
        };
        _cache.StoreDiagnostics(uri, cached);

        // Act — no server needed; cache short-circuits
        var result = await _client.GetDiagnosticsAsync("C:\\test\\test.lua");

        // Assert
        result.ShouldBeSameAs(cached);
    }

    [Test]
    public async Task GetDiagnosticsAsync_Timeout_ReturnsEmptyList()
    {
        // Arrange — file content cached so no open is attempted; 10s hardcoded timeout applies
        var testFile = "C:\\test\\test.lua";
        var uri = LspClient.PathToUri(testFile);
        _cache.StoreFileContent(uri, "local x = 1");

        // Act
        var result = await _client.GetDiagnosticsAsync(testFile);

        // Assert
        result.ShouldBeEmpty();
    }

    [Test]
    public async Task ReadLoop_UnexpectedExit_SetsStateToCrashed()
    {
        // Arrange
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Act — server closes its output → read loop hits EOF without cancellation
        _fakeServer.CloseOutput();
        await WaitUntil(() => _client.State == LspState.Crashed, TimeSpan.FromSeconds(5));

        // Assert
        _client.State.ShouldBe(LspState.Crashed);
    }

    [Test]
    public async Task DispatchMessage_WindowLogMessage_StaysReady()
    {
        // Arrange
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Act — a notification that must be ignored by the dispatch loop
        _fakeServer.SendWindowLogMessage("something went wrong");
        await Task.Delay(200);

        // Assert
        _client.State.ShouldBe(LspState.Ready);
    }

    [Test]
    public async Task DispatchMessage_UnknownNotification_StaysReady()
    {
        // Arrange
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Act — an unhandled notification hits the default case (logged, ignored)
        _fakeServer.SendUnknownNotification();
        await Task.Delay(200);

        // Assert
        _client.State.ShouldBe(LspState.Ready);
    }

    [Test]
    public async Task EnsureInitializedAsync_SameProjectTwice_IsNoop()
    {
        // Arrange
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Act — same project path while Ready → early return
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        // Assert — the process manager was only asked once
        _processManager.EnsureRunningAsyncCalls.ShouldBe(1);
    }

    [Test]
    public async Task ShutdownAsync_WhenNotStarted_IsNoop()
    {
        // Act — never initialized
        await _client.ShutdownAsync();

        // Assert
        _client.State.ShouldBe(LspState.NotStarted);
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        condition().ShouldBeTrue();
    }
}
