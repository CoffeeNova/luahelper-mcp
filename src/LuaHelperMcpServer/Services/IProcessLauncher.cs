using System.Diagnostics;

namespace LuaHelperMcpServer.Services;

public interface IProcessLauncher
{
    IProcessHandle Start(ProcessStartInfo startInfo);
}
