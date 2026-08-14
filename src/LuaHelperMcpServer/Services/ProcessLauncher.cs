using System.Diagnostics;

namespace LuaHelperMcpServer.Services;

public sealed class ProcessLauncher : IProcessLauncher
{
    public IProcessHandle Start(ProcessStartInfo startInfo)
    {
        return new ProcessHandle(new Process { StartInfo = startInfo });
    }
}
