using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Services;
using Microsoft.Extensions.Logging;

var lualspPath =
    Environment.GetEnvironmentVariable("LUAHELPER_LUALSP_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "lualsp", "win-x64", "lualsp.exe");
var pluginPath =
    Environment.GetEnvironmentVariable("LUAHELPER_PLUGIN_PATH")
    ?? Path.GetDirectoryName(lualspPath)
    ?? AppContext.BaseDirectory;

var projectPath = args.Length > 0 ? args[0] : Environment.CurrentDirectory;

if (!Directory.Exists(projectPath))
{
    Console.Error.WriteLine($"Error: Directory not found: {projectPath}");
    return 1;
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});

var processManager = new ProcessManager(loggerFactory.CreateLogger<ProcessManager>(), lualspPath);
var cache = new DiagnosticCache();
var lspClient = new LspClient(processManager, cache, loggerFactory.CreateLogger<LspClient>());

var config = new LuaHelperConfig
{
    ProjectPath = projectPath,
    PluginPath = pluginPath,
    AllEnable = true,
    CheckSyntax = true,
    CheckAnnotateType = true,
    CheckTableDuplicateKey = true,
    CheckAssignParamNum = true,
    CheckLocalDefineParamNum = true,
    CheckGotoLable = true,
    CheckFunctionDuplicateParam = true,
    CheckDuplicateIf = true,
    EnableReport = true,
};

try
{
    Console.WriteLine($"Initializing LSP for project: {projectPath}");
    await lspClient.EnsureInitializedAsync(projectPath, config);

    var luaFiles = Directory
        .EnumerateFiles(projectPath, "*.lua", SearchOption.AllDirectories)
        .Where(f => !f.Contains("\\.vscode\\") && !f.Contains("\\Tests\\"))
        .ToList();

    Console.WriteLine($"Found {luaFiles.Count} .lua files. Opening for analysis...");

    foreach (var file in luaFiles)
        await lspClient.OpenFileAsync(file);

    Console.WriteLine("Waiting for diagnostics...");
    await Task.Delay(TimeSpan.FromSeconds(3));

    var allDiagnostics = lspClient.GetAllDiagnostics();
    var total = allDiagnostics.Values.Sum(d => d.Count);

    if (total == 0)
    {
        Console.WriteLine($"\nNo warnings found in project {projectPath}");
    }
    else
    {
        var filesWithDiags = allDiagnostics.Where(kv => kv.Value.Count > 0).ToList();
        Console.WriteLine($"\n=== DIAGNOSTICS ({filesWithDiags.Count} files, {total} total) ===");

        foreach (var (uri, diagnostics) in filesWithDiags)
        {
            var filePath = uri.Replace("file:///", "").Replace("/", "\\");
            Console.WriteLine($"\n--- {filePath} ({diagnostics.Count}) ---");
            foreach (var d in diagnostics)
                Console.WriteLine(
                    $"  L{d.StartLine + 1}:{d.StartCharacter + 1} [{d.Severity}] {d.Message}"
                );
        }

        Console.WriteLine($"\n=== TOTAL: {total} diagnostics ===");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
finally
{
    await lspClient.ShutdownAsync();
    processManager.Dispose();
}

return 0;
