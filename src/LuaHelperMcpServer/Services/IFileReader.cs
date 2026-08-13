namespace LuaHelperMcpServer.Services;

public interface IFileReader
{
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
}
