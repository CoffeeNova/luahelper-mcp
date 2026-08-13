using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace LuaHelperMcpServer.Prompts;

[McpServerPromptType]
public sealed class LuaHelperPrompts
{
    [McpServerPrompt(Name = "fix_lua_warnings", Title = "Fix Lua warnings")]
    [Description("Analyze a Lua file and suggest fixes for all warnings reported by LuaHelper.")]
    public ChatMessage FixLuaWarnings(
        [Description("Absolute path to the .lua file to fix")] string filePath
    ) =>
        new(
            ChatRole.User,
            $"Analyze the Lua file at {filePath} and suggest fixes for all warnings reported by LuaHelper."
        );

    [McpServerPrompt(Name = "configure_luahelper", Title = "Configure LuaHelper")]
    [Description("Help configure luahelper.json for a Lua project.")]
    public ChatMessage ConfigureLuahelper(
        [Description("Absolute path to the project root")] string projectPath
    ) =>
        new(
            ChatRole.User,
            $"Help me configure luahelper.json for my Lua project at {projectPath}. Consider the WoW API globals that should be ignored."
        );
}
