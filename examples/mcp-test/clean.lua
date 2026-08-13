-- Clean file: must produce no diagnostics.

local function add(a, b)
    return a + b
end

local result = add(2, 3)
print(result)