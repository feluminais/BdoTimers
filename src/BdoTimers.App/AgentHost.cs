using System.Text.Json.Nodes;
using System.Windows.Threading;
using BdoTimers.Core.Agent;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

/// <summary>
/// Serves the MCP bridge's pipe while Agent access is on. Each call runs on the UI thread, where the stores' change handlers
/// expect to run, and nothing here shows a window or takes focus.
/// </summary>
public sealed class AgentHost(AppServices services, Dispatcher dispatcher, string instance = App.InstanceName) : IDisposable
{
    readonly AgentTools _tools = new(services.Timers, services.Todos, services.Settings, services.Clock, TimeZoneInfo.Local,
        new AgentHooks(DeleteTimer: id => services.Undo.DeleteTimer(id), ResetTimer: id => services.Undo.ResetTimer(id),
            DeleteBoss: id => services.Undo.DeleteBoss(id)));
    AgentPipeServer? _server;

    public bool IsServing => _server is not null;

    /// <summary>Starts or stops serving to match the Agent access setting.</summary>
    public void Apply(bool on)
    {
        if (on == IsServing) return;
        if (!on)
        {
            _server!.Dispose();
            _server = null;
            Log.Info("Agent access off");
            return;
        }
        _server = new AgentPipeServer(AgentPipe.Name(instance), CallAsync, (message, ex) => Log.Error(message, ex));
        _server.Start();
        Log.Info("Agent access on");
    }

    async Task<JsonNode> CallAsync(string tool, JsonObject args, CancellationToken ct)
    {
        try { return await dispatcher.InvokeAsync(() => _tools.Call(tool, args), DispatcherPriority.Normal, ct); }
        catch (StateSaveException ex)
        {
            services.Health.Failed("Saving", ex);
            throw;
        }
    }

    public void Dispose() => Apply(false);
}
