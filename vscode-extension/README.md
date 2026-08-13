# LuaHelper MCP Server

MCP server for Lua diagnostics powered by LuaHelper. Installs a Model Context
Protocol server that exposes the `check_lua_file` and `check_lua_project` tools
to VS Code Copilot Chat.

After installation, VS Code starts the `luahelper` MCP server automatically.
Ask Copilot to check a Lua file, e.g.:

> Check src/main.lua for Lua warnings