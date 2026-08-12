using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LuaHelperMcpServer.Tests.Integration.Services;

public class LspClientIntegrationTests
{
    private const string LualspExeName = "lualsp.exe";
    private static readonly string ExtensionPath =
        Environment.GetEnvironmentVariable("LUAHELPER_EXTENSION_PATH")
        ?? throw new InvalidOperationException(
            "LUAHELPER_EXTENSION_PATH environment variable is not set."
        );

    private static string LualspPath => Path.Combine(ExtensionPath, "server", LualspExeName);

    private ProcessManager _processManager = null!;
    private DiagnosticCache _cache = null!;
    private LspClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        if (!File.Exists(LualspPath))
            Assert.Ignore(
                $"lualsp.exe not found at {LualspPath}. Set LUAHELPER_EXTENSION_PATH environment variable."
            );

        _processManager = new ProcessManager(NullLogger<ProcessManager>.Instance, LualspPath);
        _cache = new DiagnosticCache();
        _client = new LspClient(_processManager, _cache, NullLogger<LspClient>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _processManager.Dispose();
    }

    [Test]
    public async Task CheckFile_WithWarning_ReturnsDiagnostics()
    {
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var testFile = Path.Combine(fixturesDir, "test_with_warning.lua");

        var config = new LuaHelperConfig
        {
            ProjectPath = fixturesDir,
            PluginPath = ExtensionPath,
            AllEnable = true,
            CheckSyntax = true,
            CheckAnnotateType = true,
        };

        await _client.EnsureInitializedAsync(fixturesDir, config);
        await _client.OpenFileAsync(testFile);
        var diagnostics = await _client.GetDiagnosticsAsync(testFile);

        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics[0].Message, Does.Contain("Frame").IgnoreCase);
        Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Information));
    }

    [Test]
    public async Task CheckFile_Clean_ReturnsNoDiagnostics()
    {
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var testFile = Path.Combine(fixturesDir, "test_clean.lua");

        var config = new LuaHelperConfig
        {
            ProjectPath = fixturesDir,
            PluginPath = ExtensionPath,
            AllEnable = true,
            CheckSyntax = true,
        };

        await _client.EnsureInitializedAsync(fixturesDir, config);
        await _client.OpenFileAsync(testFile);
        var diagnostics = await _client.GetDiagnosticsAsync(testFile);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task CheckMultipleFiles_AllDiagnosticsReturned()
    {
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var warningFile = Path.Combine(fixturesDir, "test_with_warning.lua");
        var cleanFile = Path.Combine(fixturesDir, "test_clean.lua");

        var config = new LuaHelperConfig
        {
            ProjectPath = fixturesDir,
            PluginPath = ExtensionPath,
            AllEnable = true,
            CheckSyntax = true,
            CheckAnnotateType = true,
        };

        await _client.EnsureInitializedAsync(fixturesDir, config);
        await _client.OpenFileAsync(warningFile);
        await _client.OpenFileAsync(cleanFile);

        var warningDiags = await _client.GetDiagnosticsAsync(warningFile);
        var cleanDiags = await _client.GetDiagnosticsAsync(cleanFile);

        Assert.That(warningDiags, Is.Not.Empty);
        Assert.That(warningDiags[0].Message, Does.Contain("Frame").IgnoreCase);
        Assert.That(warningDiags[0].Severity, Is.EqualTo(DiagnosticSeverity.Information));
        Assert.That(cleanDiags, Is.Empty);
    }
}
