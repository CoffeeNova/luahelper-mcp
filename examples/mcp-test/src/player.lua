-- Player module.
-- Planted issues:
--   - duplicate function parameter (default check: duplicate function params)
--   - duplicate table keys (default check: duplicate table keys)
--   - assignment to an undeclared global (only reported if CheckNoDefine is enabled)

local M = {}

function M.create(name, name)
    local self = {
        name = name,
        level = 1,
        level = 2,
    }
    return self
end

function M.attack(self)
    damage = 10
    print(self.name .. " attacks for " .. damage)
end

return M