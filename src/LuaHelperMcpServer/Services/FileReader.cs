namespace LuaHelperMcpServer.Services;

public sealed class FileReader : IFileReader
{
    public bool FileExists(string path) => File.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
        File.ReadAllTextAsync(path, ct);
}
