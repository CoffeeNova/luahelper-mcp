using System.Runtime.InteropServices;

namespace LuaHelperMcpServer.Extensions;

public static class LualspPathResolver
{
    public static string DefaultLualspPath
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "lualsp/win-x64/lualsp.exe";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "lualsp/osx-x64/lualsp";
            return "lualsp/linux-x64/lualsp";
        }
    }

    public static string Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = DefaultLualspPath;

        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }
}
