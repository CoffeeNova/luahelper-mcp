-- Inventory module.
-- Planted issues:
--   - duplicate table keys (default check: duplicate table keys)
--   - self-assignment (only reported if CheckSelfAssign is enabled)

local M = {}

local ITEMS = {
    { id = 1, name = "Sword" },
    { id = 2, name = "Potion", name = "Elixir" },
    { id = 2, name = "Shield" },
}

function M.find(id)
    local result = nil
    for _, item in ipairs(ITEMS) do
        if item.id == id then
            result = item
            result = item
        end
    end
    return result
end

function M.size()
    return #ITEMS
end

return M