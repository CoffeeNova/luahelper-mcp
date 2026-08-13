-- Intentionally broken file:
--   1. syntax error - the `if` statement is missing `then`
--   2. references an undefined global `undefinedGlobal`
--
-- Expect at least one syntax error from `check_lua_file`.

local x = 1

if x > 0
    x = x + 1
end

print(undefinedGlobal)