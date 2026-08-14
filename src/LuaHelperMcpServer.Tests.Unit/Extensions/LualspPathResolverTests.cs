using LuaHelperMcpServer.Extensions;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Extensions;

public class LualspPathResolverTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Resolve_NullOrWhitespace_ReturnsDefaultPath(string? path)
    {
        // Act
        var result = LualspPathResolver.Resolve(path);

        // Assert — the default is unrooted, so it is combined with the base directory
        result.ShouldBe(
            Path.Combine(AppContext.BaseDirectory, LualspPathResolver.DefaultLualspPath)
        );
    }

    [Test]
    public void Resolve_RelativePath_CombinesWithBaseDirectory()
    {
        // Act
        var result = LualspPathResolver.Resolve("lualsp/win-x64/lualsp.exe");

        // Assert
        result.ShouldBe(Path.Combine(AppContext.BaseDirectory, "lualsp/win-x64/lualsp.exe"));
    }

    [Test]
    public void Resolve_RootedPath_ReturnsAsIs()
    {
        // Arrange
        var rooted = Path.Combine(Path.GetTempPath(), "lualsp.exe");

        // Act
        var result = LualspPathResolver.Resolve(rooted);

        // Assert
        result.ShouldBe(rooted);
    }

    [Test]
    public void DefaultLualspPath_IsNeverEmpty()
    {
        // Act
        var result = LualspPathResolver.DefaultLualspPath;

        // Assert
        result.ShouldNotBeNullOrEmpty();
    }
}
