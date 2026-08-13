-- Sample Lua file referenced from the README.
-- Point the MCP tools at this file to try LuaHelper diagnostics:
--   Check examples/main.lua for Lua warnings.

local function greet(name)
    return "Hello, " .. name .. "!"
end

local player = { name = "Ada", level = 12 }

print(greet(player.name))