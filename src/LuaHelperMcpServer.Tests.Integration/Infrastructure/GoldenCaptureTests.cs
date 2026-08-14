using System.Text.Json;
using System.Text.Json.Nodes;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using LuaHelperMcpServer.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Integration.Infrastructure;

/// <summary>
/// Golden capture harness — runs real lualsp.exe and the real MCP server and
/// writes the .expected.json / .expected.txt golden files used by the
/// LspClientIntegrationTests and McpServerIntegrationTests assertions.
/// [Explicit] — run manually after a lualsp.exe version change, then review
/// the goldens before committing. Not executed by the CI pipeline.
/// </summary>
[TestFixture]
[Explicit]
public class GoldenCaptureTests
{
    private static readonly JsonSerializerOptions CamelCaseIndented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private IntegrationTestFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = IntegrationTestFixture.Instance;

    private string GoldenPath(string fileName) =>
        Path.Combine(_fixture.SourceFixturesDir, fileName);

    // Goldens must be machine-independent: absolute paths are replaced with
    // the same placeholders the integration tests normalize actual output to,
    // so a golden captured on one machine asserts correctly on any other.
    private string NormalizeForCapture(string text)
    {
        var lualspDir = Path.GetDirectoryName(_fixture.LualspPath) ?? string.Empty;
        var captureTempDir = Path.Combine(Path.GetTempPath(), "luahelper-mcp-capture-create");
        return text.Replace(_fixture.SourceFixturesDir, "FIXTURES", StringComparison.Ordinal)
            .Replace(Escaped(_fixture.SourceFixturesDir), "FIXTURES", StringComparison.Ordinal)
            .Replace(_fixture.FixturesDir, "FIXTURES", StringComparison.Ordinal)
            .Replace(Escaped(_fixture.FixturesDir), "FIXTURES", StringComparison.Ordinal)
            .Replace(lualspDir, "LUALSP_DIR", StringComparison.Ordinal)
            .Replace(Escaped(lualspDir), "LUALSP_DIR", StringComparison.Ordinal)
            .Replace(captureTempDir, "TMP", StringComparison.Ordinal)
            .Replace(Escaped(captureTempDir), "TMP", StringComparison.Ordinal);
    }

    private static string Escaped(string path) =>
        path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private void WriteJson(string fileName, JsonNode node)
    {
        var json = NormalizeForCapture(
            node.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        );
        File.WriteAllText(GoldenPath(fileName), json);
        TestContext.Progress.WriteLine($"--- {fileName} ---\n{json}");
    }

    private void WriteText(string fileName, string text)
    {
        var normalized = NormalizeForCapture(text);
        File.WriteAllText(GoldenPath(fileName), normalized);
        TestContext.Progress.WriteLine($"--- {fileName} ---\n{normalized}");
    }

    private string InnerText(JsonNode toolResponse)
    {
        var content = toolResponse["result"]!["content"]!;
        var textNode = content
            .AsArray()
            .FirstOrDefault(c => c!["type"]!.GetValue<string>() == "text");
        return textNode!["text"]!.GetValue<string>();
    }

    [Test]
    public async Task CaptureLspDiagnosticsGoldens()
    {
        // Arrange — one capture per check flag, each with its own lualsp session
        var captures = new[]
        {
            ("test_with_warning.lua", new LuaHelperConfig { CheckAnnotateType = true }),
            ("test_syntax_error.lua", new LuaHelperConfig { CheckSyntax = true }),
            ("test_undefined_global.lua", new LuaHelperConfig { CheckNoDefine = true }),
            ("test_unused_local.lua", new LuaHelperConfig { CheckLocalNoUse = true }),
            ("test_duplicate_table_key.lua", new LuaHelperConfig { CheckTableDuplicateKey = true }),
            ("test_float_eq.lua", new LuaHelperConfig { CheckFloatEq = true }),
            ("test_self_assign.lua", new LuaHelperConfig { CheckSelfAssign = true }),
        };

        foreach (var (fileName, config) in captures)
        {
            // Arrange
            var lualspPath = _fixture.LualspPath;
            using var processManager = new ProcessManager(
                NullLogger<ProcessManager>.Instance,
                lualspPath
            );
            var cache = new DiagnosticCache();
            using var client = new LspClient(processManager, cache, NullLogger<LspClient>.Instance);

            config.ProjectPath = _fixture.SourceFixturesDir;
            config.PluginPath = Path.GetDirectoryName(lualspPath) ?? string.Empty;

            // Act
            await client.EnsureInitializedAsync(_fixture.SourceFixturesDir, config);
            var filePath = Path.Combine(_fixture.SourceFixturesDir, fileName);
            await client.OpenFileAsync(filePath);
            var diagnostics = await client.GetDiagnosticsAsync(filePath);

            // Assert — every fixture is expected to produce exactly one diagnostic
            diagnostics.ShouldHaveSingleItem($"no diagnostics captured for {fileName}");

            // Capture — write the golden from the real lualsp output
            WriteText(
                fileName + ".expected.json",
                JsonSerializer.Serialize(diagnostics, CamelCaseIndented)
            );
        }
    }

    [Test]
    public void CaptureConfigGoldens()
    {
        // Arrange
        var projectDir = Path.Combine(_fixture.SourceFixturesDir, "project_with_luahelper_json");
        var configService = new ConfigService(
            Options.Create(new LuaHelperOptions { LualspPath = _fixture.LualspPath }),
            NullLogger<ConfigService>.Instance,
            new FileReader()
        );
        var defaults = new LuaHelperConfig();

        // Act — read the merged config from the real config pipeline
        var projectConfig = configService.GetConfig(projectDir).GetAwaiter().GetResult();

        // Assert — sanity-check the pipeline before capturing
        projectConfig.IgnoreModules.ShouldContain("C_Container");
        projectConfig.IgnoreFileOrDir.ShouldContain("Tests/");
        defaults.IgnoreFileOrDir.ShouldContain("one11.lua");
        defaults.IgnoreFileOrDirError.ShouldContain("one11.lua");

        // Capture — write the goldens
        WriteText(
            "project_config.expected.json",
            JsonSerializer.Serialize(projectConfig, CamelCaseIndented)
        );

        WriteText(
            "default_config.expected.json",
            JsonSerializer.Serialize(defaults, CamelCaseIndented)
        );

        WriteText(
            "luahelper_json_template.expected.json",
            JsonSerializer.Serialize(new LuahelperJsonTemplate(), CamelCaseIndented)
        );
    }

    [Test]
    public async Task CaptureMcpGoldens()
    {
        // Arrange — spawn the real MCP server
        await using var client = new McpStdioClient(
            _fixture.ServerCommand,
            _fixture.ServerArguments,
            _fixture.LualspPath,
            _fixture.RepoRoot
        );

        // Act / Assert — initialize
        var initializeResponse = await client.InitializeAsync();
        initializeResponse["result"]!["serverInfo"]!["name"]!
            .GetValue<string>()
            .ShouldBe("LuaHelperMcpServer");
        WriteJson("initialize.expected.json", initializeResponse);

        // Act / Assert — capability discovery
        var tools = await client.CallAsync("tools/list", new JsonObject());
        tools["result"]!["tools"]!.AsArray().Count.ShouldBeGreaterThanOrEqualTo(7);
        WriteJson("tools.expected.json", tools);

        var resources = await client.CallAsync("resources/list", new JsonObject());
        resources["result"]!["resources"]!
            .AsArray()
            .Any(r => r!["uri"]!.GetValue<string>() == "luahelper://config")
            .ShouldBeTrue("luahelper://config must be listed");
        WriteJson("resources.expected.json", resources);

        var prompts = await client.CallAsync("prompts/list", new JsonObject());
        prompts["result"]!["prompts"]!.AsArray().Count.ShouldBe(2);
        WriteJson("prompts.expected.json", prompts);

        // Act / Assert — version tools
        var serverVersion = await client.CallToolAsync("get_server_version", new JsonObject());
        InnerText(serverVersion).ShouldNotBeNullOrEmpty();
        WriteText("server_version.expected.txt", InnerText(serverVersion));

        var lualspVersion = await client.CallToolAsync("get_luahelper_version", new JsonObject());
        InnerText(lualspVersion).ShouldContain("lualsp");
        WriteText("lualsp_version.expected.txt", InnerText(lualspVersion));

        // Act / Assert — supported checks
        var supportedChecks = await client.CallToolAsync("get_supported_checks", new JsonObject());
        JsonNode.Parse(InnerText(supportedChecks))!.AsArray().Count.ShouldBe(21);
        WriteText("supported_checks.expected.txt", InnerText(supportedChecks));

        // Act / Assert — config tool
        var configWithJson = await client.CallToolAsync(
            "get_luahelper_config",
            new JsonObject { ["projectPath"] = _fixture.SourceFixturesDir }
        );
        JsonNode.Parse(InnerText(configWithJson))!["pluginPath"]!.ShouldNotBeNull();
        WriteText("get_luahelper_config.expected.json", InnerText(configWithJson));

        // Act / Assert — single-file diagnostics
        var checkWithWarning = await client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua"),
            }
        );
        InnerText(checkWithWarning).ShouldContain("warning(s)");
        WriteText("check_lua_file.expected.txt", InnerText(checkWithWarning));

        var checkSyntaxError = await client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_syntax_error.lua"),
            }
        );
        InnerText(checkSyntaxError).ShouldContain("[Error]");
        WriteText("check_lua_file_syntax_error.expected.txt", InnerText(checkSyntaxError));

        var checkClean = await client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_clean.lua"),
            }
        );
        InnerText(checkClean).ShouldContain("No warnings found");
        WriteText("check_lua_file_clean.expected.txt", InnerText(checkClean));

        var checkNotFound = await client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "missing_file.lua"),
            }
        );
        InnerText(checkNotFound).ShouldContain("Error: File not found");
        WriteText("check_lua_file_notfound.expected.txt", InnerText(checkNotFound));

        // Act / Assert — project diagnostics
        var checkProject = await client.CallToolAsync(
            "check_lua_project",
            new JsonObject { ["projectPath"] = _fixture.SourceFixturesDir }
        );
        InnerText(checkProject).ShouldContain("warning(s)");
        WriteText("check_lua_project.expected.txt", InnerText(checkProject));

        TestContext.Progress.WriteLine(
            "SERVER-STDERR: "
                + string.Join(
                    "\n",
                    client
                        .StderrLines.Where(l =>
                            l.Contains("error", StringComparison.OrdinalIgnoreCase)
                        )
                        .Take(30)
                )
        );

        // Act / Assert — create_luahelper_json (temp dir, never the fixtures)
        var createDir = Path.Combine(Path.GetTempPath(), "luahelper-mcp-capture-create");
        Directory.CreateDirectory(createDir);
        var createConfig = await client.CallToolAsync(
            "create_luahelper_json",
            new JsonObject { ["projectPath"] = createDir }
        );
        InnerText(createConfig).ShouldContain("Created luahelper.json");
        File.Exists(Path.Combine(createDir, "luahelper.json")).ShouldBeTrue();
        WriteText("create_luahelper_json.expected.txt", InnerText(createConfig));
        File.Copy(
            Path.Combine(createDir, "luahelper.json"),
            GoldenPath("created_luahelper_json.json"),
            overwrite: true
        );

        // Act / Assert — resource templates and reads
        var templates = await client.CallAsync("resources/templates/list", new JsonObject());
        templates["result"]!["resourceTemplates"]!.AsArray().Count.ShouldBe(1);
        WriteJson("resource_templates.expected.json", templates);

        var diagnosticsResourceUri =
            "luahelper://diagnostics/"
            + Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua");
        var diagnosticsResource = await client.ReadResourceAsync(diagnosticsResourceUri);
        diagnosticsResource["result"]!["contents"]!.AsArray().Count.ShouldBe(1);
        WriteJson("diagnostics_resource.expected.json", diagnosticsResource);

        var configResource = await client.ReadResourceAsync("luahelper://config");
        configResource["result"]!["contents"]!.AsArray().Count.ShouldBe(1);
        WriteJson("config_resource.expected.json", configResource);

        // Act / Assert — prompt get
        var promptGet = await client.GetPromptAsync(
            "fix_lua_warnings",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua"),
            }
        );
        promptGet["result"]!["messages"]!.AsArray().Count.ShouldBe(1);
        WriteJson("prompt_get.expected.json", promptGet);
    }
}
