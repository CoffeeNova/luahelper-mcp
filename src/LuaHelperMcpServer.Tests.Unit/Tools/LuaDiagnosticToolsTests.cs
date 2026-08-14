using System.Text.Json;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using LuaHelperMcpServer.Models;
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
    private IConfigService _configService = null!;
    private LuaDiagnosticTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        // Arrange
        _lspClient = Fixture.Create<ILspClient>();
        _configService = Fixture.Create<IConfigService>();
        _tools = new LuaDiagnosticTools(_lspClient, _configService);
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

    [Test]
    public async Task CheckLuaFile_FileNotFound_ReturnsErrorString()
    {
        // Act
        var result = await _tools.CheckLuaFile(
            "C:\\does\\not\\exist\\test.lua",
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("Error: File not found: C:\\does\\not\\exist\\test.lua");
        await _configService
            .DidNotReceive()
            .GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckLuaFile_ValidFile_ReturnsFormattedDiagnostics()
    {
        // Arrange
        var filePath = typeof(LuaDiagnosticToolsTests).Assembly.Location;
        _configService
            .GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LuaHelperConfig { ProjectPath = "C:\\test" });
        _lspClient
            .GetDiagnosticsAsync(filePath, Arg.Any<CancellationToken>())
            .Returns(
                new List<LuaDiagnostic>
                {
                    new()
                    {
                        StartLine = 1,
                        StartCharacter = 2,
                        Severity = DiagnosticSeverity.Warning,
                        Message = "test warning",
                    },
                }
            );

        // Act
        var result = await _tools.CheckLuaFile(filePath, CancellationToken.None);

        // Assert
        result.ShouldContain("1 warning(s) in", Case.Sensitive);
        result.ShouldContain("  L1:2 [Warning] test warning", Case.Sensitive);
        await _lspClient.Received(1).OpenFileAsync(filePath, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckLuaFile_NoDiagnostics_ReturnsNoWarnings()
    {
        // Arrange
        var filePath = typeof(LuaDiagnosticToolsTests).Assembly.Location;
        _configService
            .GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LuaHelperConfig { ProjectPath = "C:\\test" });
        _lspClient
            .GetDiagnosticsAsync(filePath, Arg.Any<CancellationToken>())
            .Returns(new List<LuaDiagnostic>());

        // Act
        var result = await _tools.CheckLuaFile(filePath, CancellationToken.None);

        // Assert
        result.ShouldContain("No warnings found in", Case.Sensitive);
    }

    [Test]
    public async Task CheckLuaFile_WhenLspCrashed_ShutsDownAndReinitializes()
    {
        // Arrange
        var filePath = typeof(LuaDiagnosticToolsTests).Assembly.Location;
        _configService
            .GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LuaHelperConfig { ProjectPath = "C:\\test" });
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
            .GetDiagnosticsAsync(filePath, Arg.Any<CancellationToken>())
            .Returns(new List<LuaDiagnostic>());

        // Act
        var result = await _tools.CheckLuaFile(filePath, CancellationToken.None);

        // Assert
        result.ShouldContain("No warnings found in", Case.Sensitive);
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
    public async Task CheckLuaProject_DirNotFound_ReturnsErrorString()
    {
        // Act
        var result = await _tools.CheckLuaProject("C:\\does\\not\\exist", CancellationToken.None);

        // Assert
        result.ShouldBe("Error: Directory not found: C:\\does\\not\\exist");
        await _configService
            .DidNotReceive()
            .GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CheckLuaProject_NoLuaFiles_ReturnsNoWarnings()
    {
        // Arrange — the test output directory contains no .lua files
        _configService
            .GetConfig(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LuaHelperConfig { ProjectPath = AppContext.BaseDirectory });

        // Act
        var result = await _tools.CheckLuaProject(AppContext.BaseDirectory, CancellationToken.None);

        // Assert
        result.ShouldBe($"No warnings found in project {AppContext.BaseDirectory}");
    }

    [Test]
    public async Task CheckLuaProject_WithDiagnostics_ReturnsSummary()
    {
        // Arrange
        using var tempDir = CreateTempProject("a.lua");
        var filePath = Path.Combine(tempDir.Path, "a.lua");
        _configService
            .GetConfig(tempDir.Path, Arg.Any<CancellationToken>())
            .Returns(new LuaHelperConfig { ProjectPath = tempDir.Path });
        _lspClient
            .GetDiagnosticsAsync(filePath, Arg.Any<CancellationToken>())
            .Returns(
                new List<LuaDiagnostic>
                {
                    new()
                    {
                        StartLine = 5,
                        Severity = DiagnosticSeverity.Warning,
                        Message = "unused local",
                    },
                }
            );

        // Act
        var result = await _tools.CheckLuaProject(tempDir.Path, CancellationToken.None);

        // Assert
        result.ShouldContain(
            "Project " + tempDir.Path + ": 1 warning(s) across 1 file(s)",
            Case.Sensitive
        );
        result.ShouldContain("--- " + tempDir.Path + "\\a.lua (1) ---", Case.Sensitive);
        result.ShouldContain("  L5:0 [Warning] unused local", Case.Sensitive);
    }

    [Test]
    public async Task CheckLuaProject_DiagnosticsTimeout_ReturnsEmptyForThatFile()
    {
        // Arrange
        using var tempDir = CreateTempProject("a.lua", "b.lua");
        var fileA = Path.Combine(tempDir.Path, "a.lua");
        var fileB = Path.Combine(tempDir.Path, "b.lua");
        _configService
            .GetConfig(tempDir.Path, Arg.Any<CancellationToken>())
            .Returns(new LuaHelperConfig { ProjectPath = tempDir.Path });
        _lspClient
            .GetDiagnosticsAsync(fileA, Arg.Any<CancellationToken>())
            .Returns(
                new List<LuaDiagnostic>
                {
                    new()
                    {
                        StartLine = 1,
                        Severity = DiagnosticSeverity.Warning,
                        Message = "a warning",
                    },
                }
            );
        _lspClient
            .GetDiagnosticsAsync(fileB, Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<List<LuaDiagnostic>>().Task);

        // Act
        var result = await _tools.CheckLuaProject(tempDir.Path, CancellationToken.None);

        // Assert — fileB timed out after 10s and contributed no warnings
        result.ShouldContain(
            "Project " + tempDir.Path + ": 1 warning(s) across 1 file(s)",
            Case.Sensitive
        );
        result.ShouldNotContain("--- " + tempDir.Path + "\\b.lua", Case.Sensitive);
    }

    [Test]
    public void BuildIgnoreSet_Null_ReturnsEmptySet()
    {
        // Act
        var set = LuaDiagnosticTools.BuildIgnoreSet(null);

        // Assert
        set.ShouldBeEmpty();
    }

    [Test]
    public void BuildIgnoreSet_TrimsTrailingSlashesAndSkipsEmptyEntries()
    {
        // Act
        var set = LuaDiagnosticTools.BuildIgnoreSet([".vscode/", "\\build\\", "/", "  ", "Tests/"]);

        // Assert — only trailing separators are trimmed; leading separators are kept as-is
        set.ShouldBe(new HashSet<string> { ".vscode", "\\build", "Tests" });
    }

    [Test]
    public void IsIgnoredFile_MatchesSubstringIgnoringCase()
    {
        // Arrange
        var set = LuaDiagnosticTools.BuildIgnoreSet(["build"]);

        // Act
        var ignored = LuaDiagnosticTools.IsIgnoredFile("C:\\Project\\BUILD\\out.lua", set);

        // Assert
        ignored.ShouldBeTrue();
    }

    [Test]
    public void IsIgnoredFile_NoMatch_ReturnsFalse()
    {
        // Arrange
        var set = LuaDiagnosticTools.BuildIgnoreSet(["build"]);

        // Act
        var ignored = LuaDiagnosticTools.IsIgnoredFile("C:\\Project\\src\\main.lua", set);

        // Assert
        ignored.ShouldBeFalse();
    }

    private sealed class TempProject : IDisposable
    {
        public string Path { get; }

        public TempProject(string path) => Path = path;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private static TempProject CreateTempProject(params string[] fileNames)
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            "luahelper-mcp-unit-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(dir);
        foreach (var name in fileNames)
            File.WriteAllText(Path.Combine(dir, name), "local x = 1");
        return new TempProject(dir);
    }
}
