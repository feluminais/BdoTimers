using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BdoTimers.Core.Agent;

/// <summary>The answer to one request over the pipe.</summary>
public sealed record AgentReply(bool Ok, JsonNode? Result, string? Error)
{
    public static AgentReply Failed(string error) => new(false, null, error);
}

/// <summary>
/// The pipe between the app and the MCP bridge: per connection one request line, <c>{"tool":…,"args":{…}}</c>, and one
/// reply line, <c>{"ok":true,"result":…}</c> or <c>{"ok":false,"error":…}</c>. Only the same Windows user can connect.
/// </summary>
public static class AgentPipe
{
    public const int MaxLineChars = 256 * 1024;

    /// <summary>The pipe of the app instance named <paramref name="instance"/> (<c>BdoTimers</c>, or <c>BdoTimers-Dev</c> for a dev build).</summary>
    public static string Name(string instance) => $"{instance}.Agent";

    internal static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>One line, or null at the end of the stream; throws when the line is longer than <see cref="MaxLineChars"/>.</summary>
    internal static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken ct)
    {
        var line = new StringBuilder();
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), ct) > 0)
        {
            if (buffer[0] == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= MaxLineChars) throw new InvalidDataException("The message is too long.");
            line.Append(buffer[0]);
        }
        return line.Length > 0 ? line.ToString() : null;
    }
}

/// <summary>Serves <see cref="AgentPipe"/> requests with <paramref name="handler"/>. Failures other than <see cref="AgentToolException"/> are
/// reported to <paramref name="log"/> and answered with a generic error.</summary>
public sealed class AgentPipeServer(string pipeName, Func<string, JsonObject, CancellationToken, Task<JsonNode>> handler,
    Action<string, Exception?>? log = null) : IDisposable
{
    static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5), HandlerTimeout = TimeSpan.FromSeconds(20);

    readonly CancellationTokenSource _stop = new();
    Task? _accepting;
    int _disposed;

    public void Start() => _accepting ??= Task.Run(AcceptAsync);

    async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                var connected = pipe;
                pipe = null;
                _ = Task.Run(() => ServeAsync(connected));
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
            catch (Exception ex)
            {
                log?.Invoke("Agent pipe couldn't accept a connection", ex);
                try { await Task.Delay(TimeSpan.FromSeconds(2), _stop.Token); }
                catch (Exception delay) when (delay is OperationCanceledException or ObjectDisposedException) { }
            }
            finally { pipe?.Dispose(); }
        }
    }

    async Task ServeAsync(NamedPipeServerStream pipe)
    {
        await using var _ = pipe;
        try
        {
            using var reader = new StreamReader(pipe, AgentPipe.Utf8, false, 4096, leaveOpen: true);
            using var read = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            read.CancelAfter(RequestTimeout);
            var line = await AgentPipe.ReadLineAsync(reader, read.Token);
            var reply = line is null ? AgentReply.Failed("Empty request.") : await ReplyAsync(line);
            var text = new JsonObject { ["ok"] = reply.Ok, ["result"] = reply.Result, ["error"] = reply.Error }.ToJsonString();
            await pipe.WriteAsync(AgentPipe.Utf8.GetBytes(text + "\n"), _stop.Token);
            await pipe.FlushAsync(_stop.Token);
            // The client closes after reading the reply; closing first could discard what it hasn't read yet.
            using var closed = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            closed.CancelAfter(RequestTimeout);
            await reader.ReadToEndAsync(closed.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidDataException or ObjectDisposedException) { }
        catch (Exception ex) { log?.Invoke("Agent pipe request failed", ex); }
    }

    async Task<AgentReply> ReplyAsync(string line)
    {
        JsonObject? request;
        try { request = JsonNode.Parse(line) as JsonObject; }
        catch (JsonException) { request = null; }
        if (request?["tool"] is not JsonValue tool || !tool.TryGetValue<string>(out var name))
            return AgentReply.Failed("Not a tool request.");
        if (request["args"] is not (null or JsonObject)) return AgentReply.Failed("args must be an object.");
        var args = (request["args"] as JsonObject)?.DeepClone().AsObject() ?? new JsonObject();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(HandlerTimeout);
            return new(true, await handler(name, args, timeout.Token), null);
        }
        catch (AgentToolException ex) { return AgentReply.Failed(ex.Message); }
        catch (OperationCanceledException) { return AgentReply.Failed("BDO Timers took too long to answer."); }
        catch (Exception ex)
        {
            log?.Invoke($"Agent tool {name} failed", ex);
            return AgentReply.Failed(ex is IOException ? ex.Message : "The tool failed inside BDO Timers.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _stop.Cancel();
        // So the pipe is gone, and a new call finds nothing to connect to, once this returns.
        try { _accepting?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { }
        _stop.Dispose();
    }
}

public static class AgentPipeClient
{
    public const string AppNotAvailable = "BDO Timers isn't running, or Agent access is off (Settings → General).";

    public static async Task<AgentReply> CallAsync(string pipeName, string tool, JsonObject args, TimeSpan connectTimeout, CancellationToken ct)
    {
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try { await pipe.ConnectAsync(connectTimeout, ct); }
        catch (Exception ex) when (ex is TimeoutException or IOException) { return AgentReply.Failed(AppNotAvailable); }
        try
        {
            var request = new JsonObject { ["tool"] = tool, ["args"] = args.DeepClone() }.ToJsonString();
            await pipe.WriteAsync(AgentPipe.Utf8.GetBytes(request + "\n"), ct);
            await pipe.FlushAsync(ct);
            using var reader = new StreamReader(pipe, AgentPipe.Utf8, false, 4096, leaveOpen: true);
            var line = await AgentPipe.ReadLineAsync(reader, ct);
            if (line is null) return AgentReply.Failed("BDO Timers closed the connection without answering.");
            var reply = JsonNode.Parse(line) as JsonObject ?? throw new JsonException();
            return reply["ok"]?.GetValue<bool>() == true
                ? new(true, reply["result"]?.DeepClone(), null)
                : AgentReply.Failed(reply["error"]?.GetValue<string>() ?? "The tool failed.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or InvalidOperationException)
        {
            return AgentReply.Failed($"BDO Timers sent an unreadable answer ({ex.Message}).");
        }
    }
}
