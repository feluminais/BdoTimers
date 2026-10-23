using BdoTimers.Core.Agent;

namespace BdoTimers.Core.Tests;

public class AgentSetupTests
{
    [Fact]
    public void The_command_adds_the_bridge_beside_the_app()
    {
        Assert.Equal("""claude mcp add bdo-timers -- "C:\Apps\BdoTimers\BdoTimers.Mcp.exe" """.TrimEnd(),
            AgentSetup.ClaudeCodeCommand(@"C:\Apps\BdoTimers", AgentSetup.DefaultInstance));
    }

    [Fact]
    public void A_dev_build_names_its_instance()
    {
        Assert.EndsWith("""BdoTimers.Mcp.exe" --instance BdoTimers-Dev""", AgentSetup.ClaudeCodeCommand(@"C:\Apps\BdoTimers", "BdoTimers-Dev"));
    }
}
