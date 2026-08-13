-- Small utilities module.
-- Planted issues:
--   - annotation with an unknown type (default check: annotation type)
--   - unused local (only reported if CheckLocalNoUse is enabled)

---@type Player
local player = nil

local M = {}

local unusedLocal = 42

function M.log(message)
    print("[log] " .. message)
end

function M.tick(frame)
    local nextFrame = frame + 1
    return frame
end

return M