namespace BdoTimers.Core.Agent;

/// <summary>A tool couldn't do what was asked; the message goes back to the agent as it is.</summary>
public sealed class AgentToolException(string message) : Exception(message);
