using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using BdoTimers.Core.Agent;

namespace BdoTimers.Core.Tests;

public sealed class AgentPipeTests : IDisposable
{
    readonly string _pipe = AgentPipe.Name("BdoTimers-Test-" + Guid.NewGuid().ToString("N"));
    readonly List<(string Message, Exception? Error)> _logged = [];
    AgentPipeServer? _server;

    public void Dispose() => _server?.Dispose();

    void Serve(Func<string, JsonObject, CancellationToken, Task<JsonNode>> handler)
    {
        _server = new AgentPipeServer(_pipe, handler, (message, error) => { lock (_logged) _logged.Add((message, error)); });
        _server.Start();
    }

    Task<AgentReply> Call(string tool, string args = "{}") =>
        AgentPipeClient.CallAsync(_pipe, tool, JsonNode.Parse(args)!.AsObject(), TimeSpan.FromSeconds(5), CancellationToken.None);

    [Fact]
    public void The_pipe_is_named_after_the_instance()
    {
        Assert.Equal("BdoTimers-Dev.Agent", AgentPipe.Name("BdoTimers-Dev"));
    }

    [Fact]
    public async Task A_call_reaches_the_handler_and_its_result_comes_back()
    {
        Serve((tool, args, _) => Task.FromResult<JsonNode>(new JsonObject { ["tool"] = tool, ["echo"] = args["x"]!.DeepClone() }));

        var reply = await Call("get_status", """{"x":[1,"ü"]}""");

        Assert.True(reply.Ok);
        Assert.Equal("get_status", reply.Result!["tool"]!.GetValue<string>());
        Assert.Equal("""[1,"ü"]""", reply.Result["echo"]!.ToJsonString(new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public async Task Many_calls_in_a_row_and_at_once_all_get_answered()
    {
        Serve(async (_, args, _) =>
        {
            await Task.Delay(10);
            return new JsonObject { ["n"] = args["n"]!.DeepClone() };
        });

        var replies = await Task.WhenAll(Enumerable.Range(0, 20).Select(n => Call("t", $$"""{"n":{{n}}}""")));

        Assert.Equal(Enumerable.Range(0, 20), replies.Select(r => r.Result!["n"]!.GetValue<int>()));
        Assert.Equal(7, (await Call("t", """{"n":7}""")).Result!["n"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_tool_error_comes_back_as_its_message()
    {
        Serve((_, _, _) => throw new AgentToolException("No timer matches 'x'."));

        var reply = await Call("control_timer");

        Assert.False(reply.Ok);
        Assert.Equal("No timer matches 'x'.", reply.Error);
        Assert.Empty(_logged);
    }

    [Fact]
    public async Task Any_other_failure_is_logged_and_answered_without_its_details()
    {
        Serve((_, _, _) => throw new InvalidOperationException("secret path C:\\Users\\x"));

        var reply = await Call("get_status");

        Assert.False(reply.Ok);
        Assert.DoesNotContain("secret", reply.Error);
        var logged = Assert.Single(_logged);
        Assert.Contains("secret", logged.Error!.Message);
    }

    [Fact]
    public async Task Without_a_server_the_call_says_the_app_is_not_available()
    {
        var reply = await AgentPipeClient.CallAsync(_pipe, "get_status", new JsonObject(), TimeSpan.FromMilliseconds(200), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(AgentPipeClient.AppNotAvailable, reply.Error);
    }

    [Fact]
    public async Task After_the_server_stops_the_call_says_the_app_is_not_available()
    {
        Serve((_, _, _) => Task.FromResult<JsonNode>(new JsonObject()));
        Assert.True((await Call("get_status")).Ok);

        _server!.Dispose();

        var reply = await AgentPipeClient.CallAsync(_pipe, "get_status", new JsonObject(), TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Assert.Equal(AgentPipeClient.AppNotAvailable, reply.Error);
    }

    [Theory]
    [InlineData("not json", "Not a tool request.")]
    [InlineData("""{"args":{}}""", "Not a tool request.")]
    [InlineData("""{"tool":"x","args":[1]}""", "args must be an object.")]
    public async Task A_malformed_request_is_refused(string request, string error)
    {
        Serve((_, _, _) => Task.FromResult<JsonNode>(new JsonObject()));

        await using var pipe = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(5000);
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(request + "\n"));
        using var reader = new StreamReader(pipe);
        var reply = JsonNode.Parse((await reader.ReadLineAsync())!)!;

        Assert.False(reply["ok"]!.GetValue<bool>());
        Assert.Equal(error, reply["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_request_over_the_limit_gets_no_answer_and_the_server_carries_on()
    {
        Serve((_, _, _) => Task.FromResult<JsonNode>(new JsonObject { ["alive"] = true }));

        await using (var pipe = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            await pipe.ConnectAsync(5000);
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(new string('x', AgentPipe.MaxLineChars + 10)));
            await pipe.FlushAsync();
            using var reader = new StreamReader(pipe);
            Assert.True(string.IsNullOrEmpty(await reader.ReadLineAsync()));
        }

        Assert.True((await Call("get_status")).Result!["alive"]!.GetValue<bool>());
    }
}
