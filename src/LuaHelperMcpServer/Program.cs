using LuaHelperMcpServer.Extensions;
using LuaHelperMcpServer.Models;
using LuaHelperMcpServer.Prompts;
using LuaHelperMcpServer.Resources;
using LuaHelperMcpServer.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

var envLualspPath = Environment.GetEnvironmentVariable("LUAHELPER_LUALSP_PATH");
if (!string.IsNullOrEmpty(envLualspPath))
    builder.Configuration["LuaHelper:LualspPath"] = envLualspPath;

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder
    .Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<LuaDiagnosticTools>()
    .WithTools<ConfigTools>()
    .WithTools<VersionTools>()
    .WithResources<DiagnosticResources>()
    .WithPrompts<LuaHelperPrompts>();

builder.Services.Configure<LuaHelperOptions>(builder.Configuration.GetSection("LuaHelper"));
builder.Services.AddLuaHelperServices();

await builder.Build().RunAsync();
