using LuaHelperMcpServer.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var lualspPath =
    Environment.GetEnvironmentVariable("LUAHELPER_LUALSP_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "lualsp", "win-x64", "lualsp.exe");

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

builder.Services.AddLuaHelperServices(lualspPath);

await builder.Build().RunAsync();
