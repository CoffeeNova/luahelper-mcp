-- Entry point of the sample project.
-- Planted issue:
--   - assignment param count mismatch (default check: assignment param count)

local utils = require("src.utils")
local player = require("src.player")

local hero = player.create("Ada")
utils.log("Created hero " .. hero.name)

local width, height = 640

for frame = 1, 100 do
    utils.tick(frame)
end

utils.log("Simulation finished")