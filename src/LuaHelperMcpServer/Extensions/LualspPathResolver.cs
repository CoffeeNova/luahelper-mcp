namespace LuaHelperMcpServer.Extensions;

public static class LualspPathResolver
{
    private const string DefaultLualspPath = "lualsp/win-x64/lualsp.exe";

    public static string Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = DefaultLualspPath;

        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }
}
