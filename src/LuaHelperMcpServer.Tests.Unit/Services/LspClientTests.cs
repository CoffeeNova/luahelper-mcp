using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LuaHelperMcpServer.Tests.Unit.Services;

public class LspClientTests
{
    private FakeLspServer _fakeServer = null!;
    private MockProcessManager _processManager = null!;
    private DiagnosticCache _cache = null!;
    private LspClient _client = null!;
    private Mock<IFileReader> _fileReaderMock = null!;

    [SetUp]
    public void SetUp()
    {
        _fakeServer = new FakeLspServer();
        _processManager = new MockProcessManager(_fakeServer);
        _cache = new DiagnosticCache();
        _fileReaderMock = new Mock<IFileReader>();
        _client = new LspClient(
            _processManager,
            _cache,
            NullLogger<LspClient>.Instance,
            _fileReaderMock.Object
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
        _fakeServer.Start();

        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        Assert.That(_client.State, Is.EqualTo(LspState.Ready));
    }

    [Test]
    public async Task OpenFileAsync_SendsDidOpen()
    {
        var testFile = "C:\\test\\test.lua";
        _fileReaderMock.Setup(f => f.FileExists(testFile)).Returns(true);
        _fileReaderMock
            .Setup(f => f.ReadAllTextAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("local x = 1");

        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );
        await _client.OpenFileAsync(testFile);

        var uri = LspClient.PathToUri(testFile);
        Assert.That(_cache.GetFileContent(uri), Is.EqualTo("local x = 1"));
    }

    [Test]
    public async Task GetDiagnosticsAsync_ReceivesPublishDiagnostics()
    {
        var testFile = "C:\\test\\test.lua";
        _fileReaderMock.Setup(f => f.FileExists(testFile)).Returns(true);
        _fileReaderMock
            .Setup(f => f.ReadAllTextAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("local x = 1");

        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );
        await _client.OpenFileAsync(testFile);

        var diagnostics = await _client.GetDiagnosticsAsync(testFile);

        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics[0].Message, Does.Contain("Test warning"));
        Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        Assert.That(diagnostics[0].WarningType, Is.EqualTo(1));
        Assert.That(diagnostics[0].StartLine, Is.EqualTo(0));
        Assert.That(diagnostics[0].StartCharacter, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAllDiagnostics_ReturnsAllDiagnostics()
    {
        var testFile = "C:\\test\\test.lua";
        _fileReaderMock.Setup(f => f.FileExists(testFile)).Returns(true);
        _fileReaderMock
            .Setup(f => f.ReadAllTextAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync("local x = 1");

        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );
        await _client.OpenFileAsync(testFile);
        await _client.GetDiagnosticsAsync(testFile);

        var all = _client.GetAllDiagnostics();

        Assert.That(all, Is.Not.Empty);
        Assert.That(all.Values.Sum(d => d.Count), Is.EqualTo(1));
    }

    [Test]
    public async Task ShutdownAsync_ChangesStateToStopped()
    {
        _fakeServer.Start();
        await _client.EnsureInitializedAsync(
            "C:\\test",
            new LuaHelperConfig { PluginPath = "C:\\test" }
        );

        await _client.ShutdownAsync();

        Assert.That(_client.State, Is.EqualTo(LspState.Stopped));
    }
}
