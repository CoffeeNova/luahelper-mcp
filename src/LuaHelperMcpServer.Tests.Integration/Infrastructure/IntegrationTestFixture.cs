using NUnit.Framework;

namespace LuaHelperMcpServer.Tests.Integration.Infrastructure;

/// <summary>
/// Resolves the real binaries (lualsp.exe, LuaHelperMcpServer) once per test run.
/// Missing binaries are a hard failure with a clear message — never a skip.
/// </summary>
public sealed class IntegrationTestFixture
{
    private static readonly Lazy<IntegrationTestFixture> LazyInstance = new(
        () => new IntegrationTestFixture(),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public static IntegrationTestFixture Instance => LazyInstance.Value;

    public string RepoRoot { get; }

    public string LualspPath { get; }

    public string ServerCommand { get; }

    public string? ServerArguments { get; }

    public string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public string SourceFixturesDir =>
        Path.Combine(RepoRoot, "src", "LuaHelperMcpServer.Tests.Integration", "Fixtures");

    private IntegrationTestFixture()
    {
        RepoRoot = FindRepoRoot();

        LualspPath = ResolveLualspPath();

        (ServerCommand, ServerArguments) = ResolveServerCommand();
    }

    private string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "luahelper-mcp.sln")))
                return dir.FullName;
            if (Directory.Exists(Path.Combine(dir.FullName, ".github")))
                return dir.FullName;
            dir = dir.Parent;
        }

        Assert.Fail(
            $"Could not locate the repository root from {AppContext.BaseDirectory}. "
                + "Run the tests from the repository checkout."
        );
        return string.Empty;
    }

    private string ResolveLualspPath()
    {
        var candidates = new List<string>();

        var envPath = Environment.GetEnvironmentVariable("LUAHELPER_LUALSP_PATH");
        if (!string.IsNullOrEmpty(envPath))
            candidates.Add(envPath);

        var extensionPath = Environment.GetEnvironmentVariable("LUAHELPER_EXTENSION_PATH");
        if (!string.IsNullOrEmpty(extensionPath))
            candidates.Add(Path.Combine(extensionPath, "server", "lualsp.exe"));

        candidates.Add(Path.Combine(RepoRoot, "lualsp", "win-x64", "lualsp.exe"));

        var found = candidates.FirstOrDefault(File.Exists);
        if (found != null)
            return found;

        Assert.Fail(
            "lualsp.exe not found. Tried: "
                + string.Join("; ", candidates)
                + ". Run .github/tools/fetch-lualsp.ps1 first."
        );
        return string.Empty;
    }

    private (string command, string? arguments) ResolveServerCommand()
    {
        var envValue = Environment.GetEnvironmentVariable("LUAHELPER_MCP_SERVER_PATH");
        if (!string.IsNullOrEmpty(envValue))
        {
            var parts = envValue.Split(';', 2);
            if (parts.Length == 2 && File.Exists(parts[1]))
                return (parts[0], parts[1]);
            if (File.Exists(parts[0]))
                return (parts[0], null);
        }

        var releaseDll = Path.Combine(
            RepoRoot,
            "src",
            "LuaHelperMcpServer",
            "bin",
            "Release",
            "net10.0",
            "LuaHelperMcpServer.dll"
        );
        var debugDll = Path.Combine(
            RepoRoot,
            "src",
            "LuaHelperMcpServer",
            "bin",
            "Debug",
            "net10.0",
            "LuaHelperMcpServer.dll"
        );

        var dll =
            File.Exists(releaseDll) ? releaseDll
            : File.Exists(debugDll) ? debugDll
            : null;
        if (dll != null)
            return ("dotnet", dll);

        Assert.Fail(
            "LuaHelperMcpServer binary not found. Tried LUAHELPER_MCP_SERVER_PATH, "
                + $"{releaseDll}, {debugDll}. Build the solution first (.github/tools/build.ps1 -Configuration Release)."
        );
        return (string.Empty, null);
    }
}
