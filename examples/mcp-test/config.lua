-- Configuration table with planted issues:
--   - duplicate table keys (default check: duplicate table keys)
--   - self-assignment (only reported if CheckSelfAssign is enabled)

local config = {
    title = "Mini Game",
    title = "Mini Game v2",
    port = 8080,
    volume = 0.5,
}

config.volume = config.volume

local fallback = config.title or "untitled"

return config