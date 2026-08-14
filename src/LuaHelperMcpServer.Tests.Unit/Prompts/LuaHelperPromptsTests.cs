using LuaHelperMcpServer.Prompts;
using Shouldly;

namespace LuaHelperMcpServer.Tests.Unit.Prompts;

public class LuaHelperPromptsTests
{
    [Test]
    public void FixLuaWarnings_ReturnsUserMessageWithPath()
    {
        // Arrange
        var prompts = new LuaHelperPrompts();

        // Act
        var message = prompts.FixLuaWarnings("C:\\addons\\MyAddon\\core.lua");

        // Assert
        message.Role.ToString().ShouldBe("user");
        message.Text.ShouldContain("C:\\addons\\MyAddon\\core.lua", Case.Sensitive);
        message.Text.ShouldContain("suggest fixes", Case.Sensitive);
    }

    [Test]
    public void ConfigureLuahelper_ReturnsUserMessageWithPath()
    {
        // Arrange
        var prompts = new LuaHelperPrompts();

        // Act
        var message = prompts.ConfigureLuahelper("C:\\addons\\MyAddon");

        // Assert
        message.Role.ToString().ShouldBe("user");
        message.Text.ShouldContain("C:\\addons\\MyAddon", Case.Sensitive);
        message.Text.ShouldContain("luahelper.json", Case.Sensitive);
        message.Text.ShouldContain("WoW API globals", Case.Sensitive);
    }
}
