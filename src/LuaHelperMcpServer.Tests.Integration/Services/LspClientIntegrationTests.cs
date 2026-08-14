using System.Text.Json;
using System.Text.Json.Nodes;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Integration.Services;

/// <summary>
/// LSP-layer integration tests against the real lualsp.exe.
/// Diagnostics are asserted exactly against the golden files captured by
/// <see cref="GoldenCaptureTests"/>. Missing binaries are a hard failure
/// (never a skip).
/// </summary>
public class LspClientIntegrationTests
{
    private static readonly JsonSerializerOptions CamelCaseIndented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private IntegrationTestFixture _fixture = null!;
    private ProcessManager _processManager = null!;
    private DiagnosticCache _cache = null!;
    private LspClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = IntegrationTestFixture.Instance;
        _processManager = new ProcessManager(
            NullLogger<ProcessManager>.Instance,
            _fixture.LualspPath
        );
        _cache = new DiagnosticCache();
        _client = new LspClient(_processManager, _cache, NullLogger<LspClient>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _processManager?.Dispose();
    }

    private LuaHelperConfig Config(Action<LuaHelperConfig>? configure = null)
    {
        var config = new LuaHelperConfig
        {
            ProjectPath = _fixture.SourceFixturesDir,
            PluginPath = Path.GetDirectoryName(_fixture.LualspPath) ?? string.Empty,
        };
        configure?.Invoke(config);
        return config;
    }

    private async Task<List<LuaDiagnostic>> CheckFileAsync(string fileName, LuaHelperConfig config)
    {
        var filePath = Path.Combine(_fixture.SourceFixturesDir, fileName);
        await _client.EnsureInitializedAsync(_fixture.SourceFixturesDir, config);
        await _client.OpenFileAsync(filePath);
        return await _client.GetDiagnosticsAsync(filePath);
    }

    private void AssertDiagnosticsMatchGolden(List<LuaDiagnostic> diagnostics, string fixtureName)
    {
        var expected = NormalizeDiagnosticUris(
            GoldenAssert.ReadGolden(fixtureName + ".expected.json")
        );
        var actual = NormalizeDiagnosticUris(
            JsonSerializer.Serialize(diagnostics, CamelCaseIndented)
        );
        GoldenAssert.AssertJsonEquals(expected, actual);
    }

    private static string NormalizeDiagnosticUris(string json)
    {
        var array = JsonNode.Parse(json)!.AsArray();
        foreach (var item in array)
        {
            var uri = item!["uri"]!.GetValue<string>();
            item["uri"] = Path.GetFileName(uri);
        }
        return array.ToJsonString();
    }

    [Test]
    public async Task CheckFile_WithWarning_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_with_warning.lua",
            Config(c => c.CheckAnnotateType = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_with_warning.lua");
    }

    [Test]
    public async Task CheckFile_SyntaxError_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_syntax_error.lua",
            Config(c => c.CheckSyntax = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_syntax_error.lua");
        diagnostics.ShouldHaveSingleItem();
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task CheckFile_UndefinedGlobal_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_undefined_global.lua",
            Config(c => c.CheckNoDefine = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_undefined_global.lua");
    }

    [Test]
    public async Task CheckFile_LocalNoUse_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_unused_local.lua",
            Config(c => c.CheckLocalNoUse = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_unused_local.lua");
    }

    [Test]
    public async Task CheckFile_DuplicateTableKey_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_duplicate_table_key.lua",
            Config(c => c.CheckTableDuplicateKey = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_duplicate_table_key.lua");
    }

    [Test]
    public async Task CheckFile_FloatEq_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_float_eq.lua",
            Config(c => c.CheckFloatEq = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_float_eq.lua");
    }

    [Test]
    public async Task CheckFile_SelfAssign_MatchesGolden()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync(
            "test_self_assign.lua",
            Config(c => c.CheckSelfAssign = true)
        );

        // Assert
        AssertDiagnosticsMatchGolden(diagnostics, "test_self_assign.lua");
    }

    [Test]
    public async Task CheckFile_Clean_ReturnsNoDiagnostics()
    {
        // Arrange / Act
        var diagnostics = await CheckFileAsync("test_clean.lua", Config());

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckMultipleFiles_AllDiagnosticsReturned()
    {
        // Arrange
        var warningFile = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua");
        var cleanFile = Path.Combine(_fixture.SourceFixturesDir, "test_clean.lua");
        var config = Config(c => c.CheckAnnotateType = true);

        // Act
        await _client.EnsureInitializedAsync(_fixture.SourceFixturesDir, config);
        await _client.OpenFileAsync(warningFile);
        await _client.OpenFileAsync(cleanFile);
        var warningDiags = await _client.GetDiagnosticsAsync(warningFile);
        var cleanDiags = await _client.GetDiagnosticsAsync(cleanFile);

        // Assert
        AssertDiagnosticsMatchGolden(warningDiags, "test_with_warning.lua");
        cleanDiags.ShouldBeEmpty();
    }

    [Test]
    public async Task LuahelperJson_IgnoredModules_ProduceNoDiagnostics()
    {
        // Arrange
        var projectDir = CreateTempProject(ignoreModules: true);
        try
        {
            var config = await GetConfigForProject(projectDir, _fixture.LualspPath);
            var luaFile = Path.Combine(projectDir, "Main.lua");

            // Act
            await _client.EnsureInitializedAsync(projectDir, config);
            await _client.OpenFileAsync(luaFile);
            var diagnostics = await _client.GetDiagnosticsAsync(luaFile);

            // Assert
            diagnostics.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task LuahelperJson_MissingIgnoreModules_FlagsUndefinedGlobal()
    {
        // Arrange
        var projectDir = CreateTempProject(ignoreModules: false);
        try
        {
            var config = await GetConfigForProject(projectDir, _fixture.LualspPath);
            var luaFile = Path.Combine(projectDir, "Main.lua");

            // Act
            await _client.EnsureInitializedAsync(projectDir, config);
            await _client.OpenFileAsync(luaFile);
            var diagnostics = await _client.GetDiagnosticsAsync(luaFile);

            // Assert
            diagnostics.ShouldNotBeEmpty();
            diagnostics
                .Any(d => d.Message.Contains("C_Container"))
                .ShouldBeTrue("Expected an undefined-variable diagnostic for C_Container");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task Reinitialize_SameProject_IsNoop()
    {
        // Arrange
        var config = Config(c => c.CheckAnnotateType = true);
        await CheckFileAsync("test_with_warning.lua", config);

        // Act — same project again: must not re-initialize
        await _client.EnsureInitializedAsync(_fixture.SourceFixturesDir, config);

        // Assert — state unchanged and diagnostics still available
        _client.ProjectPath.ShouldBe(_fixture.SourceFixturesDir);
        var diagnostics = await _client.GetDiagnosticsAsync(
            Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua")
        );
        AssertDiagnosticsMatchGolden(diagnostics, "test_with_warning.lua");
    }

    [Test]
    public async Task Reinitialize_DifferentProject_Reinitializes()
    {
        // Arrange
        await CheckFileAsync("test_with_warning.lua", Config(c => c.CheckAnnotateType = true));
        var projectDir = CreateTempProject(ignoreModules: false);
        try
        {
            var config = await GetConfigForProject(projectDir, _fixture.LualspPath);

            // Act
            await _client.EnsureInitializedAsync(projectDir, config);

            // Assert — the client now serves the new project
            _client.ProjectPath.ShouldBe(projectDir);
            await _client.OpenFileAsync(Path.Combine(projectDir, "Main.lua"));
            var diagnostics = await _client.GetDiagnosticsAsync(
                Path.Combine(projectDir, "Main.lua")
            );
            diagnostics.ShouldNotBeEmpty();
            diagnostics
                .Any(d => d.Message.Contains("C_Container"))
                .ShouldBeTrue("Expected a diagnostic from the newly initialized project");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task Shutdown_ThenReopen_Works()
    {
        // Arrange
        var config = Config(c => c.CheckAnnotateType = true);
        await CheckFileAsync("test_with_warning.lua", config);

        // Act
        await _client.ShutdownAsync();
        await _client.EnsureInitializedAsync(_fixture.SourceFixturesDir, config);

        // Assert — a fresh lualsp session serves diagnostics again
        var diagnostics = await _client.GetDiagnosticsAsync(
            Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua")
        );
        AssertDiagnosticsMatchGolden(diagnostics, "test_with_warning.lua");
    }

    [Test]
    public async Task CrashRecovery_AfterProcessExit_RestartsAndRechecks()
    {
        // Arrange
        var config = Config(c => c.CheckSyntax = true);
        await CheckFileAsync("test_with_warning.lua", Config(c => c.CheckAnnotateType = true));
        var syntaxErrorPath = Path.Combine(_fixture.SourceFixturesDir, "test_syntax_error.lua");

        // Act — kill lualsp mid-session, then check an uncached file
        _processManager.ForceKill();
        await _client.ShutdownAsync();
        await _client.EnsureInitializedAsync(_fixture.SourceFixturesDir, config);
        await _client.OpenFileAsync(syntaxErrorPath);
        var diagnostics = await _client.GetDiagnosticsAsync(syntaxErrorPath);

        // Assert — the restarted lualsp analyzed the file correctly
        AssertDiagnosticsMatchGolden(diagnostics, "test_syntax_error.lua");
    }

    private static string CreateTempProject(bool ignoreModules)
    {
        var projectDir = Path.Combine(
            Path.GetTempPath(),
            "luahelper-it-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(projectDir);

        var ignoredModules = ignoreModules
            ? """
                ["C_Container", "C_UnitAuras", "C_Timer", "C_AddOns", "CreateFrame", "GetTime", "print", "pairs", "ipairs", "tinsert", "tremove", "table", "string", "math", "tostring", "tonumber", "type", "error", "assert", "select", "unpack", "next", "rawget", "rawset", "setmetatable", "getmetatable"]
                """
            : "[]";

        File.WriteAllText(
            Path.Combine(projectDir, "luahelper.json"),
            $$"""
            { "ShowWarnFlag": 1, "IgnoreModules": {{ignoredModules}} }
            """
        );
        File.WriteAllText(
            Path.Combine(projectDir, "Main.lua"),
            """
            local frame = CreateFrame("Frame")
            frame:SetSize(100, 100)
            local count = C_Container.GetContainerNumSlots(0)
            local aura = C_UnitAuras.GetPlayerAuraBySpellID(1)
            print(frame, count, aura)
            """
        );
        return projectDir;
    }

    private static async Task<LuaHelperConfig> GetConfigForProject(
        string projectDir,
        string lualspPath
    )
    {
        var options = Options.Create(new LuaHelperOptions { LualspPath = lualspPath });
        var configService = new ConfigService(
            options,
            NullLogger<ConfigService>.Instance,
            new FileReader()
        );
        return await configService.GetConfig(projectDir);
    }
}
