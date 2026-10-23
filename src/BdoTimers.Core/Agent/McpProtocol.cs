using System.Text.Json;
using System.Text.Json.Nodes;

namespace BdoTimers.Core.Agent;

/// <summary>What a tool call gave back: text for the agent, and whether it is an error.</summary>
public sealed record ToolOutcome(bool IsError, string Text);

/// <summary>
/// The server side of the Model Context Protocol over newline-delimited JSON-RPC (stdio): <c>initialize</c>, <c>ping</c>,
/// <c>tools/list</c> and <c>tools/call</c>. Notifications get no reply.
/// </summary>
public sealed class McpProtocol(string serverName, string serverVersion, IReadOnlyList<AgentToolInfo> tools,
    Func<string, JsonObject, CancellationToken, Task<ToolOutcome>> callTool)
{
    /// <summary>Newest first; a client asking for one of these gets it, else the newest.</summary>
    public static readonly IReadOnlyList<string> SupportedVersions = ["2025-06-18", "2025-03-26", "2024-11-05"];

    const int ParseError = -32700, InvalidRequest = -32600, MethodNotFound = -32601, InvalidParams = -32602;

    const string Instructions = "Controls the BDO Timers app on this PC. Times in results are UTC and the PC's local time. "
        + "The app has to be running with Agent access switched on in its Settings.";

    /// <summary>The reply to one message, or null when it needs none.</summary>
    public async Task<string?> HandleAsync(string message, CancellationToken ct)
    {
        JsonNode? parsed;
        try { parsed = JsonNode.Parse(message); }
        catch (JsonException) { return Error(null, ParseError, "Invalid JSON."); }
        if (parsed is not JsonObject request || request["method"] is not JsonValue methodValue || !methodValue.TryGetValue<string>(out var method))
            return Error((parsed as JsonObject)?["id"], InvalidRequest, "Not a JSON-RPC request.");
        // A message without an id is a notification, whatever its method.
        if (request["id"] is not { } id) return null;
        id = id.DeepClone();
        var parameters = request["params"] as JsonObject;
        try
        {
            return method switch
            {
                "initialize" => Result(id, Initialize(parameters)),
                "ping" => Result(id, new JsonObject()),
                "tools/list" => Result(id, ListTools()),
                "tools/call" => await CallAsync(id, parameters, ct),
                _ => Error(id, MethodNotFound, $"Method not found: {method}"),
            };
        }
        catch (InvalidParamsException ex) { return Error(id, InvalidParams, ex.Message); }
    }

    JsonObject Initialize(JsonObject? parameters)
    {
        var asked = parameters?["protocolVersion"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        return new JsonObject
        {
            ["protocolVersion"] = asked is not null && SupportedVersions.Contains(asked) ? asked : SupportedVersions[0],
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = serverName, ["version"] = serverVersion },
            ["instructions"] = Instructions,
        };
    }

    JsonObject ListTools() => new()
    {
        ["tools"] = new JsonArray(tools.Select(t => (JsonNode)new JsonObject
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["inputSchema"] = t.InputSchema.DeepClone(),
            ["annotations"] = new JsonObject
            {
                ["readOnlyHint"] = t.ReadOnly,
                ["destructiveHint"] = t.Destructive,
                ["idempotentHint"] = t.ReadOnly,
                ["openWorldHint"] = false,
            },
        }).ToArray()),
    };

    async Task<string> CallAsync(JsonNode id, JsonObject? parameters, CancellationToken ct)
    {
        if (parameters?["name"] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out var name))
            throw new InvalidParamsException("A tool name is required.");
        if (tools.All(t => t.Name != name)) throw new InvalidParamsException($"Unknown tool: {name}");
        if (parameters["arguments"] is not (null or JsonObject)) throw new InvalidParamsException("arguments must be an object.");
        var arguments = (parameters["arguments"] as JsonObject)?.DeepClone().AsObject() ?? new JsonObject();
        ToolOutcome outcome;
        try { outcome = await callTool(name, arguments, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { outcome = new(true, $"The tool failed: {ex.Message}"); }
        return Result(id, new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = outcome.Text }),
            ["isError"] = outcome.IsError,
        });
    }

    static string Result(JsonNode id, JsonNode result) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result }.ToJsonString();

    static string Error(JsonNode? id, int code, string message) => new JsonObject
    {
        ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(),
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    }.ToJsonString();

    sealed class InvalidParamsException(string message) : Exception(message);
}
