namespace BdoTimers.Core.Agent;

public static class AgentSetup
{
    public const string BridgeFileName = "BdoTimers.Mcp.exe";

    /// <summary>The instance a normal install runs as; any other, such as a dev build's, has to be named to the bridge.</summary>
    public const string DefaultInstance = "BdoTimers";

    /// <summary>The Claude Code command that adds the bridge in <paramref name="appDirectory"/> as an MCP server.</summary>
    public static string ClaudeCodeCommand(string appDirectory, string instance)
    {
        var bridge = Path.Combine(appDirectory, BridgeFileName);
        var command = $"claude mcp add bdo-timers -- \"{bridge}\"";
        return instance == DefaultInstance ? command : $"{command} --instance {instance}";
    }
}
