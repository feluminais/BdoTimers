using System.Reflection;
using System.Text;
using BdoTimers.Core.Agent;

// An MCP server on stdio. It answers the protocol itself and hands each tool call to the running BDO Timers app over its pipe.
var instance = "BdoTimers";
for (var i = 0; i < args.Length - 1; i++)
    if (args[i] == "--instance") instance = args[i + 1];

var pipe = AgentPipe.Name(instance);
var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0.0";
var protocol = new McpProtocol("bdo-timers", version, AgentCatalog.Tools, async (tool, arguments, ct) =>
{
    var reply = await AgentPipeClient.CallAsync(pipe, tool, arguments, TimeSpan.FromSeconds(2), ct);
    return reply.Ok ? new ToolOutcome(false, reply.Result?.ToJsonString() ?? "{}") : new ToolOutcome(true, reply.Error ?? "The tool failed.");
});

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

var utf8 = new UTF8Encoding(false);
using var input = new StreamReader(Console.OpenStandardInput(), utf8);
using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" };
try
{
    while (await input.ReadLineAsync(shutdown.Token) is { } line)
    {
        if (line.Length == 0) continue;
        if (await protocol.HandleAsync(line, shutdown.Token) is { } reply) await output.WriteLineAsync(reply.AsMemory(), shutdown.Token);
    }
}
catch (OperationCanceledException) { }
catch (IOException) { }
