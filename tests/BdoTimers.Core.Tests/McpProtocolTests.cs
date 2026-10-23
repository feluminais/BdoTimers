using System.Text.Json.Nodes;
using BdoTimers.Core.Agent;

namespace BdoTimers.Core.Tests;

public class McpProtocolTests
{
    readonly List<(string Name, string Args)> _calls = [];
    ToolOutcome _outcome = new(false, "ok");

    McpProtocol Protocol() => new("bdo-timers", "1.2.3", AgentCatalog.Tools, (name, args, _) =>
    {
        _calls.Add((name, args.ToJsonString()));
        return Task.FromResult(_outcome);
    });

    async Task<JsonObject> Send(string message) =>
        JsonNode.Parse((await Protocol().HandleAsync(message, CancellationToken.None))!)!.AsObject();

    [Fact]
    public async Task Initialize_answers_with_the_asked_version_when_it_is_supported()
    {
        var reply = await Send("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"x","version":"1"}}}""");

        Assert.Equal(1, reply["id"]!.GetValue<int>());
        var result = reply["result"]!;
        Assert.Equal("2025-03-26", result["protocolVersion"]!.GetValue<string>());
        Assert.Equal("bdo-timers", result["serverInfo"]!["name"]!.GetValue<string>());
        Assert.Equal("1.2.3", result["serverInfo"]!["version"]!.GetValue<string>());
        Assert.NotNull(result["capabilities"]!["tools"]);
        Assert.NotNull(result["instructions"]);
    }

    [Fact]
    public async Task Initialize_offers_the_newest_version_for_one_it_does_not_know()
    {
        var reply = await Send("""{"jsonrpc":"2.0","id":"a","method":"initialize","params":{"protocolVersion":"2099-01-01"}}""");

        Assert.Equal(McpProtocol.SupportedVersions[0], reply["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.Equal("a", reply["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_message_without_an_id_is_a_notification_and_gets_no_reply()
    {
        Assert.Null(await Protocol().HandleAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""", CancellationToken.None));
        Assert.Null(await Protocol().HandleAsync("""{"jsonrpc":"2.0","method":"nothing/known"}""", CancellationToken.None));
    }

    [Fact]
    public async Task Ping_answers_with_an_empty_result()
    {
        var reply = await Send("""{"jsonrpc":"2.0","id":7,"method":"ping"}""");

        Assert.Empty(reply["result"]!.AsObject());
        Assert.Equal("2.0", reply["jsonrpc"]!.GetValue<string>());
    }

    [Fact]
    public async Task Tools_list_has_every_tool_with_its_schema_and_hints()
    {
        var reply = await Send("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        var tools = reply["result"]!["tools"]!.AsArray();
        Assert.Equal(AgentCatalog.Tools.Select(t => t.Name), tools.Select(t => t!["name"]!.GetValue<string>()));
        Assert.All(tools, t =>
        {
            Assert.Equal("object", t!["inputSchema"]!["type"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(t["description"]!.GetValue<string>()));
        });
        var upcoming = tools.Single(t => t!["name"]!.GetValue<string>() == "get_upcoming")!;
        Assert.True(upcoming["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        var delete = tools.Single(t => t!["name"]!.GetValue<string>() == "delete_timer")!;
        Assert.False(delete["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.True(delete["annotations"]!["destructiveHint"]!.GetValue<bool>());
        Assert.Equal(["timer"], delete["inputSchema"]!["required"]!.AsArray().Select(r => r!.GetValue<string>()));
    }

    [Fact]
    public async Task Tools_call_runs_the_tool_and_returns_its_text()
    {
        _outcome = new(false, """{"count":0}""");

        var reply = await Send("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_timers","arguments":{"bosses":true}}}""");

        Assert.Equal([("list_timers", """{"bosses":true}""")], _calls);
        var result = reply["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        var content = Assert.Single(result["content"]!.AsArray())!;
        Assert.Equal("text", content["type"]!.GetValue<string>());
        Assert.Equal("""{"count":0}""", content["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_call_without_arguments_gets_an_empty_object()
    {
        await Send("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_status"}}""");

        Assert.Equal([("get_status", "{}")], _calls);
    }

    [Fact]
    public async Task A_failed_tool_is_a_result_flagged_as_an_error()
    {
        _outcome = new(true, "No timer matches 'x'.");

        var reply = await Send("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"control_timer","arguments":{}}}""");

        Assert.Null(reply["error"]);
        Assert.True(reply["result"]!["isError"]!.GetValue<bool>());
        Assert.Equal("No timer matches 'x'.", reply["result"]!["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_tool_that_throws_becomes_an_error_result()
    {
        var protocol = new McpProtocol("s", "1", AgentCatalog.Tools, (_, _, _) => throw new InvalidOperationException("boom"));

        var reply = JsonNode.Parse((await protocol.HandleAsync("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_status"}}""", CancellationToken.None))!)!;

        Assert.True(reply["result"]!["isError"]!.GetValue<bool>());
        Assert.Contains("boom", reply["result"]!["content"]![0]!["text"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"format_disk"}}""", -32602)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{}}""", -32602)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_status","arguments":[1]}}""", -32602)]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":"resources/list"}""", -32601)]
    [InlineData("""{"jsonrpc":"2.0","id":1}""", -32600)]
    [InlineData("""[1,2]""", -32600)]
    [InlineData("""{not json""", -32700)]
    public async Task Bad_messages_get_a_json_rpc_error(string message, int code)
    {
        var reply = await Send(message);

        Assert.Equal(code, reply["error"]!["code"]!.GetValue<int>());
        Assert.Null(reply["result"]);
        Assert.Empty(_calls);
    }
}
