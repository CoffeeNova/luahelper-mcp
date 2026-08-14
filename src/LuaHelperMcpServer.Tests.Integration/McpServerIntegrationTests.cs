using System.Text.Json;
using System.Text.Json.Nodes;
using LuaHelperMcpServer.Tests.Integration.Infrastructure;
using NUnit.Framework;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Integration;

/// <summary>
/// MCP-layer end-to-end tests against the real LuaHelperMcpServer binary.
/// Every API surface is exercised: initialize, tools/list, resources/list,
/// resources/templates/list, prompts/list, all 7 tools, both resources and
/// both prompts. Machine-specific paths are normalized on both sides before
/// comparison; everything else is asserted exactly against goldens.
/// </summary>
public class McpServerIntegrationTests
{
    private IntegrationTestFixture _fixture = null!;
    private McpStdioClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _fixture = IntegrationTestFixture.Instance;
        _client = new McpStdioClient(
            _fixture.ServerCommand,
            _fixture.ServerArguments,
            _fixture.LualspPath,
            _fixture.RepoRoot
        );
        await _client.InitializeAsync();
    }

    [TearDown]
    public async Task TearDown() => await _client.DisposeAsync();

    private static string InnerText(JsonNode response)
    {
        var content = response["result"]!["content"]!.AsArray();
        var text = content.FirstOrDefault(c => c!["type"]!.GetValue<string>() == "text");
        text.ShouldNotBeNull("tools/call response must contain a text content block");
        return text!["text"]!.GetValue<string>();
    }

    private static string Escaped(string path) =>
        path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private string NormalizePaths(string text) =>
        text.Replace(_fixture.SourceFixturesDir, "FIXTURES", StringComparison.Ordinal)
            .Replace(Escaped(_fixture.SourceFixturesDir), "FIXTURES", StringComparison.Ordinal)
            .Replace(_fixture.FixturesDir, "FIXTURES", StringComparison.Ordinal)
            .Replace(Escaped(_fixture.FixturesDir), "FIXTURES", StringComparison.Ordinal)
            .Replace(
                Path.GetDirectoryName(_fixture.LualspPath) ?? string.Empty,
                "LUALSP_DIR",
                StringComparison.Ordinal
            )
            .Replace(
                Escaped(Path.GetDirectoryName(_fixture.LualspPath) ?? string.Empty),
                "LUALSP_DIR",
                StringComparison.Ordinal
            );

    private string NormalizeTempPaths(string text, string tempDir)
    {
        var captureTempDir = Path.Combine(Path.GetTempPath(), "luahelper-mcp-capture-create");
        return NormalizePaths(text)
            .Replace(tempDir, "TMP", StringComparison.Ordinal)
            .Replace(Escaped(tempDir), "TMP", StringComparison.Ordinal)
            .Replace(captureTempDir, "TMP", StringComparison.Ordinal)
            .Replace(Escaped(captureTempDir), "TMP", StringComparison.Ordinal);
    }

    private static string NormalizeConfigPaths(string text)
    {
        var config = JsonNode.Parse(text)!.AsObject();
        config["projectPath"] = "FIXTURES";
        config["pluginPath"] = "LUALSP_DIR";
        return config.ToJsonString();
    }

    private void AssertTextGolden(string goldenName, JsonNode toolResponse)
    {
        var expected = NormalizePaths(GoldenAssert.ReadGolden(goldenName)).TrimEnd();
        var actual = NormalizePaths(InnerText(toolResponse)).TrimEnd();
        actual.ShouldBe(expected, $"Golden mismatch for {goldenName}");
    }

    private void AssertResultGolden(string goldenName, JsonNode response)
    {
        var expected = JsonNode.Parse(GoldenAssert.ReadGolden(goldenName))!["result"]!;
        GoldenAssert.AssertJsonEquals(expected.ToJsonString(), response["result"]!.ToJsonString());
    }

    private static string CreateTempDir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), name + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // --- 6.5.1 Capability discovery -----------------------------------------

    [Test]
    public void Initialize_ReturnsServerInfoAndCapabilities()
    {
        // Arrange
        var expected = JsonNode.Parse(GoldenAssert.ReadGolden("initialize.expected.json"))![
            "result"
        ]!;

        // Act
        var initialize = _client.InitializeAsync().GetAwaiter().GetResult();

        // Assert
        GoldenAssert.AssertJsonEquals(
            expected.ToJsonString(),
            initialize["result"]!.ToJsonString()
        );
    }

    [Test]
    public async Task ToolsList_ExposesAllSevenTools()
    {
        // Act
        var tools = await _client.CallAsync("tools/list", new JsonObject());

        // Assert
        AssertResultGolden("tools.expected.json", tools);
    }

    [Test]
    public async Task ResourcesList_ExposesDiagnosticsAndConfig()
    {
        // Act
        var resources = await _client.CallAsync("resources/list", new JsonObject());

        // Assert
        AssertResultGolden("resources.expected.json", resources);
    }

    [Test]
    public async Task ResourceTemplates_ExposesDiagnosticsTemplate()
    {
        // Act
        var templates = await _client.CallAsync("resources/templates/list", new JsonObject());

        // Assert
        AssertResultGolden("resource_templates.expected.json", templates);
    }

    [Test]
    public async Task PromptsList_ExposesBothPrompts()
    {
        // Act
        var prompts = await _client.CallAsync("prompts/list", new JsonObject());

        // Assert
        AssertResultGolden("prompts.expected.json", prompts);
    }

    // --- 6.5.2 Tools --------------------------------------------------------

    [Test]
    public async Task GetServerVersion_MatchesGolden()
    {
        // Act
        var response = await _client.CallToolAsync("get_server_version", new JsonObject());

        // Assert
        AssertTextGolden("server_version.expected.txt", response);
    }

    [Test]
    public async Task GetLuahelperVersion_MatchesBundledLualsp()
    {
        // Act
        var response = await _client.CallToolAsync("get_luahelper_version", new JsonObject());

        // Assert
        AssertTextGolden("lualsp_version.expected.txt", response);
    }

    [Test]
    public async Task GetSupportedChecks_ReturnsExactCheckList()
    {
        // Act
        var response = await _client.CallToolAsync("get_supported_checks", new JsonObject());

        // Assert
        AssertTextGolden("supported_checks.expected.txt", response);
    }

    [Test]
    public async Task GetLuahelperConfig_ProjectWithLuahelperJson_MatchesGolden()
    {
        // Arrange
        var expected = NormalizeConfigPaths(
            GoldenAssert.ReadGolden("project_config.expected.json")
        );

        // Act
        var response = await _client.CallToolAsync(
            "get_luahelper_config",
            new JsonObject
            {
                ["projectPath"] = Path.Combine(
                    _fixture.SourceFixturesDir,
                    "project_with_luahelper_json"
                ),
            }
        );

        // Assert
        var actual = NormalizeConfigPaths(InnerText(response));
        GoldenAssert.AssertJsonEquals(expected, actual);
    }

    [Test]
    public async Task GetLuahelperConfig_ProjectWithoutLuahelperJson_ReturnsDefaults()
    {
        // Arrange
        var tempDir = CreateTempDir("luahelper-mcp-config-defaults-");
        var expected = NormalizeConfigPaths(
            GoldenAssert.ReadGolden("default_config.expected.json")
        );
        try
        {
            // Act
            var response = await _client.CallToolAsync(
                "get_luahelper_config",
                new JsonObject { ["projectPath"] = tempDir }
            );

            // Assert
            var actual = NormalizeConfigPaths(InnerText(response));
            GoldenAssert.AssertJsonEquals(expected, actual);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task CreateLuahelperJson_CreatesFileWithExactContent()
    {
        // Arrange
        var tempDir = CreateTempDir("luahelper-mcp-create-");
        var expected = NormalizeTempPaths(
            GoldenAssert.ReadGolden("create_luahelper_json.expected.txt"),
            tempDir
        );
        try
        {
            // Act
            var response = await _client.CallToolAsync(
                "create_luahelper_json",
                new JsonObject { ["projectPath"] = tempDir }
            );

            // Assert
            var actual = NormalizeTempPaths(InnerText(response), tempDir);
            actual.ShouldBe(expected);

            var createdFile = File.ReadAllText(Path.Combine(tempDir, "luahelper.json"));
            var expectedFile = GoldenAssert.ReadGolden("created_luahelper_json.json");
            GoldenAssert.AssertJsonEquals(expectedFile, createdFile);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task CreateLuahelperJson_InvalidDir_ReturnsError()
    {
        // Arrange
        var missingDir = Path.Combine(
            Path.GetTempPath(),
            "luahelper-mcp-missing-" + Guid.NewGuid().ToString("N")
        );

        // Act
        var response = await _client.CallToolAsync(
            "create_luahelper_json",
            new JsonObject { ["projectPath"] = missingDir }
        );

        // Assert
        NormalizePaths(InnerText(response))
            .ShouldBe($"Error: Directory not found: {NormalizePaths(missingDir)}");
    }

    [Test]
    public async Task CheckLuaFile_WithWarning_MatchesGolden()
    {
        // Act
        var response = await _client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua"),
            }
        );

        // Assert
        AssertTextGolden("check_lua_file.expected.txt", response);
    }

    [Test]
    public async Task CheckLuaFile_Clean_ReturnsNoWarnings()
    {
        // Act
        var response = await _client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_clean.lua"),
            }
        );

        // Assert
        AssertTextGolden("check_lua_file_clean.expected.txt", response);
    }

    [Test]
    public async Task CheckLuaFile_FileNotFound_ReturnsError()
    {
        // Arrange
        var missingFile = Path.Combine(_fixture.SourceFixturesDir, "missing_file.lua");

        // Act
        var response = await _client.CallToolAsync(
            "check_lua_file",
            new JsonObject { ["filePath"] = missingFile }
        );

        // Assert
        NormalizePaths(InnerText(response))
            .ShouldBe($"Error: File not found: FIXTURES\\missing_file.lua");
    }

    [Test]
    public async Task CheckLuaFile_SyntaxError_MatchesGolden()
    {
        // Act
        var response = await _client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_syntax_error.lua"),
            }
        );

        // Assert
        AssertTextGolden("check_lua_file_syntax_error.expected.txt", response);
    }

    [Test]
    public async Task CheckLuaProject_WithWarnings_MatchesGolden()
    {
        // Act
        var response = await _client.CallToolAsync(
            "check_lua_project",
            new JsonObject { ["projectPath"] = _fixture.SourceFixturesDir }
        );

        // Assert
        AssertTextGolden("check_lua_project.expected.txt", response);
    }

    [Test]
    public async Task CheckLuaProject_CleanProject_ReturnsNoWarnings()
    {
        // Arrange
        var tempDir = CreateTempDir("luahelper-mcp-clean-project-");
        try
        {
            // Act
            var response = await _client.CallToolAsync(
                "check_lua_project",
                new JsonObject { ["projectPath"] = tempDir }
            );

            // Assert
            NormalizeTempPaths(InnerText(response), tempDir)
                .ShouldBe("No warnings found in project TMP");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task CheckLuaProject_DirNotFound_ReturnsError()
    {
        // Arrange
        var missingDir = Path.Combine(
            Path.GetTempPath(),
            "luahelper-mcp-missing-" + Guid.NewGuid().ToString("N")
        );

        // Act
        var response = await _client.CallToolAsync(
            "check_lua_project",
            new JsonObject { ["projectPath"] = missingDir }
        );

        // Assert
        NormalizePaths(InnerText(response))
            .ShouldBe($"Error: Directory not found: {NormalizePaths(missingDir)}");
    }

    // --- 6.5.3 Resources ----------------------------------------------------

    [Test]
    public async Task ReadDiagnosticsResource_WithWarning_MatchesGolden()
    {
        // Arrange
        var filePath = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua");
        var uri = "luahelper://diagnostics/" + filePath;
        var expected = NormalizeDiagnosticUris(
            GoldenAssert.ReadGolden("test_with_warning.lua.expected.json")
        );

        // Act
        var response = await _client.ReadResourceAsync(uri);

        // Assert
        var contents = response["result"]!["contents"]!.AsArray();
        contents.ShouldHaveSingleItem();
        contents[0]!["uri"]!.GetValue<string>().ShouldBe(uri);

        var actual = NormalizeDiagnosticUris(contents[0]!["text"]!.GetValue<string>());
        GoldenAssert.AssertJsonEquals(expected, actual);
    }

    [Test]
    public async Task ReadDiagnosticsResource_FileNotFound_ReturnsError()
    {
        // Arrange
        var missingFile = Path.Combine(_fixture.SourceFixturesDir, "missing_file.lua");

        // Act
        var response = await _client.ReadResourceAsync("luahelper://diagnostics/" + missingFile);

        // Assert
        var errorMessage =
            response["error"]?["message"]?.GetValue<string>() ?? ExtractResourceText(response);
        errorMessage.ShouldNotBeNullOrEmpty("resource read must return an error");
        NormalizePaths(errorMessage).ShouldContain($"File not found: FIXTURES\\missing_file.lua");
    }

    [Test]
    public async Task ReadConfigResource_BeforeProject_ReturnsDefaults()
    {
        // Arrange
        var expected = NormalizeConfigPaths(
            GoldenAssert.ReadGolden("default_config.expected.json")
        );

        // Act
        var response = await _client.ReadResourceAsync("luahelper://config");

        // Assert
        var actual = NormalizeConfigPaths(ExtractResourceText(response));
        GoldenAssert.AssertJsonEquals(expected, actual);
    }

    [Test]
    public async Task ReadConfigResource_AfterProjectCheck_MatchesProjectConfig()
    {
        // Arrange — activate a project so the resource resolves its config
        var expected = NormalizeConfigPaths(
            GoldenAssert.ReadGolden("get_luahelper_config.expected.json")
        );
        await _client.CallToolAsync(
            "check_lua_file",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua"),
            }
        );

        // Act
        var response = await _client.ReadResourceAsync("luahelper://config");

        // Assert
        var actual = NormalizeConfigPaths(ExtractResourceText(response));
        GoldenAssert.AssertJsonEquals(expected, actual);
    }

    // --- 6.5.4 Prompts ------------------------------------------------------

    [Test]
    public async Task GetFixLuaWarningsPrompt_ReturnsExactMessage()
    {
        // Arrange
        var expected = JsonNode.Parse(
            NormalizePaths(GoldenAssert.ReadGolden("prompt_get.expected.json"))
        )!["result"]!;

        // Act
        var response = await _client.GetPromptAsync(
            "fix_lua_warnings",
            new JsonObject
            {
                ["filePath"] = Path.Combine(_fixture.SourceFixturesDir, "test_with_warning.lua"),
            }
        );

        // Assert
        var actual = JsonNode.Parse(NormalizePaths(response.ToJsonString()))!["result"]!;
        GoldenAssert.AssertJsonEquals(expected.ToJsonString(), actual.ToJsonString());
    }

    [Test]
    public async Task GetConfigureLuahelperPrompt_ReturnsExactMessage()
    {
        // Act
        var response = await _client.GetPromptAsync(
            "configure_luahelper",
            new JsonObject { ["projectPath"] = _fixture.SourceFixturesDir }
        );

        // Assert
        var messages = response["result"]!["messages"]!.AsArray();
        messages.ShouldHaveSingleItem();
        NormalizePaths(messages[0]!["content"]!["text"]!.GetValue<string>())
            .ShouldBe(
                "Help me configure luahelper.json for my Lua project at FIXTURES. Consider the WoW API globals that should be ignored."
            );
    }

    private static string ExtractResourceText(JsonNode response)
    {
        var result = response["result"];
        if (result == null)
            return string.Empty;

        var contents = result["contents"] ?? result["content"];
        if (contents is not JsonArray contentArray || contentArray.Count == 0)
            return string.Empty;

        var text = contentArray[0]!["text"];
        return text?.GetValue<string>() ?? string.Empty;
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
}
