using LuaHelperMcpServer.Tools;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Tools;

public class VersionToolsTests
{
    private readonly VersionTools _tools = new();

    [Test]
    public void GetServerVersion_ReturnsSemverWithoutSuffix()
    {
        // Act
        var result = _tools.GetServerVersion(CancellationToken.None).GetAwaiter().GetResult();

        // Assert
        result.ShouldMatch(@"^\d+\.\d+\.\d+$");
    }
}
