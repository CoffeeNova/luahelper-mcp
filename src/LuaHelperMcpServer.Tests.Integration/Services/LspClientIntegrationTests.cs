using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Integration.Services;

public class LspClientIntegrationTests
{
    private const string LualspExeName = "lualsp.exe";
    private static readonly string? ExtensionPath = Environment.GetEnvironmentVariable(
        "LUAHELPER_EXTENSION_PATH"
    );

    private static string LualspPath =>
        Path.Combine(ExtensionPath ?? string.Empty, "server", LualspExeName);

    private ProcessManager _processManager = null!;
    private DiagnosticCache _cache = null!;
    private LspClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        if (string.IsNullOrEmpty(ExtensionPath) || !File.Exists(LualspPath))
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
        _client?.Dispose();
        _processManager?.Dispose();
    }

    [Test]
    public async Task CheckFile_WithWarning_ReturnsDiagnostics()
    {
        // Arrange
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var testFile = Path.Combine(fixturesDir, "test_with_warning.lua");
        var config = new LuaHelperConfig
        {
            ProjectPath = fixturesDir,
            PluginPath = ExtensionPath!,
            AllEnable = true,
            CheckSyntax = true,
            CheckAnnotateType = true,
        };

        // Act
        await _client.EnsureInitializedAsync(fixturesDir, config);
        await _client.OpenFileAsync(testFile);
        var diagnostics = await _client.GetDiagnosticsAsync(testFile);

        // Assert
        diagnostics.ShouldNotBeEmpty();
        diagnostics[0].Message.ShouldContain("Frame", Case.Insensitive);
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Information);
    }

    [Test]
    public async Task CheckFile_Clean_ReturnsNoDiagnostics()
    {
        // Arrange
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var testFile = Path.Combine(fixturesDir, "test_clean.lua");
        var config = new LuaHelperConfig
        {
            ProjectPath = fixturesDir,
            PluginPath = ExtensionPath!,
            AllEnable = true,
            CheckSyntax = true,
        };

        // Act
        await _client.EnsureInitializedAsync(fixturesDir, config);
        await _client.OpenFileAsync(testFile);
        var diagnostics = await _client.GetDiagnosticsAsync(testFile);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckMultipleFiles_AllDiagnosticsReturned()
    {
        // Arrange
        var fixturesDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var warningFile = Path.Combine(fixturesDir, "test_with_warning.lua");
        var cleanFile = Path.Combine(fixturesDir, "test_clean.lua");
        var config = new LuaHelperConfig
        {
            ProjectPath = fixturesDir,
            PluginPath = ExtensionPath!,
            AllEnable = true,
            CheckSyntax = true,
            CheckAnnotateType = true,
        };

        // Act
        await _client.EnsureInitializedAsync(fixturesDir, config);
        await _client.OpenFileAsync(warningFile);
        await _client.OpenFileAsync(cleanFile);
        var warningDiags = await _client.GetDiagnosticsAsync(warningFile);
        var cleanDiags = await _client.GetDiagnosticsAsync(cleanFile);

        // Assert
        warningDiags.ShouldNotBeEmpty();
        warningDiags[0].Message.ShouldContain("Frame", Case.Insensitive);
        warningDiags[0].Severity.ShouldBe(DiagnosticSeverity.Information);
        cleanDiags.ShouldBeEmpty();
    }

    [Test]
    public async Task LuahelperJson_IgnoredModules_ProduceNoDiagnostics()
    {
        // Arrange
        var projectDir = CreateTempProject(ignoreModules: true);
        try
        {
            var config = await GetConfigForProject(projectDir);
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
            var config = await GetConfigForProject(projectDir);
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

    private static async Task<LuaHelperConfig> GetConfigForProject(string projectDir)
    {
        var options = Options.Create(new LuaHelperOptions { LualspPath = LualspPath });
        var configService = new ConfigService(
            options,
            NullLogger<ConfigService>.Instance,
            new FileReader()
        );
        return await configService.GetConfig(projectDir);
    }
}
