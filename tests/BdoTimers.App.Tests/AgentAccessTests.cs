using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Agent;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Tests;

public class AgentAccessTests
{
    [Fact]
    public void Agent_access_is_off_until_it_is_saved_on_in_Settings() => WpfTest.Run(() => WithServices(services =>
    {
        using var main = new MainViewModel(services);
        var panel = new SettingsPanelViewModel(services);
        Assert.False(panel.AgentAccess);
        Assert.False(panel.HasChanges);

        panel.AgentAccess = true;

        Assert.True(panel.HasChanges);
        Assert.False(services.Settings.Current.AgentAccess);
        main.OpenPanel(panel);
        main.FinishPanel().GetAwaiter().GetResult();
        Assert.True(services.Settings.Current.AgentAccess);
        Assert.True(new SettingsPanelViewModel(services).AgentAccess);
    }));

    [Fact]
    public void The_Settings_row_is_found_by_search_and_offers_the_command_only_while_on() => WpfTest.Run(() => WithServices(services =>
    {
        var panel = new SettingsPanel { DataContext = new SettingsPanelViewModel(services) };
        var window = new Window { Content = panel, Width = 500, Height = 600, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            WpfTest.Drain();
            ((TextBox)panel.FindName("SearchBox")).Text = "mcp";
            var items = (StackPanel)panel.FindName("SettingsItems");
            var row = Assert.Single(items.Children.OfType<FrameworkElement>(), r => r is not TextBlock && r.Visibility == Visibility.Visible);
            var link = Descendants(row).OfType<Button>().Single(b => b.Content as string == "Copy MCP command");
            var toggle = Descendants(row).OfType<ToggleButton>().Single();
            Assert.Equal("Agent access", AutomationProperties.GetName(toggle));
            Assert.False(link.IsVisible);

            toggle.IsChecked = true;
            WpfTest.Drain();

            Assert.True(link.IsVisible);
        }
        finally { window.Close(); }
    }));

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    [Fact]
    public void The_host_serves_the_pipe_only_while_it_is_on_and_changes_go_through_the_apps_stores() => WpfTest.Run(() => WithServices(services =>
    {
        var instance = "BdoTimers-Test-" + Guid.NewGuid().ToString("N");
        using var host = new AgentHost(services, Dispatcher.CurrentDispatcher, instance);
        Assert.False(host.IsServing);
        Assert.Equal(AgentPipeClient.AppNotAvailable, Call(instance, "get_status").Error);

        host.Apply(true);
        Assert.True(host.IsServing);
        var made = Call(instance, "create_countdown", """{"name":"Tea","minutes":5}""");
        Assert.True(made.Ok);
        var id = made.Result!["id"]!.GetValue<string>();
        Assert.Contains(services.Timers.Current.Timers, t => t.Name == "Tea" && t.Id.ToString() == id);

        var removed = Call(instance, "delete_timer", $$"""{"timer":"{{id}}"}""");
        Assert.True(removed.Ok);
        Assert.DoesNotContain(services.Timers.Current.Timers, t => t.Name == "Tea");
        Assert.True(services.Undo.CanUndo);

        Assert.False(Call(instance, "delete_timer", """{"timer":"Farm"}""").Ok);
        Assert.Contains(services.Timers.Current.Timers, t => t.Name == "Farm");

        host.Apply(false);
        Assert.False(host.IsServing);
        Assert.Equal(AgentPipeClient.AppNotAvailable, Call(instance, "get_status").Error);
    }));

    [Fact]
    public void The_bridge_exe_speaks_MCP_on_stdio_and_reaches_the_running_app() => WpfTest.Run(() => WithServices(services =>
    {
        var instance = "BdoTimers-Test-" + Guid.NewGuid().ToString("N");
        using var host = new AgentHost(services, Dispatcher.CurrentDispatcher, instance);
        host.Apply(true);
        using var bridge = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, AgentSetup.BridgeFileName), ["--instance", instance])
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = new System.Text.UTF8Encoding(false), StandardInputEncoding = new System.Text.UTF8Encoding(false),
        })!;
        try
        {
            JsonNode Exchange(string request)
            {
                var reply = Pump(Task.Run(() =>
                {
                    bridge.StandardInput.WriteLine(request);
                    bridge.StandardInput.Flush();
                    return bridge.StandardOutput.ReadLine();
                }).WaitAsync(TimeSpan.FromSeconds(20)));
                return JsonNode.Parse(reply!)!;
            }

            var init = Exchange("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""");
            Assert.Equal("bdo-timers", init["result"]!["serverInfo"]!["name"]!.GetValue<string>());
            var tools = Exchange("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""")["result"]!["tools"]!.AsArray();
            Assert.Equal(AgentCatalog.Tools.Count, tools.Count);

            var made = Exchange("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"create_countdown","arguments":{"name":"Tea","minutes":5,"start":true}}}""")["result"]!;
            Assert.False(made["isError"]!.GetValue<bool>());
            Assert.Equal("Tea", JsonNode.Parse(made["content"]![0]!["text"]!.GetValue<string>())!["name"]!.GetValue<string>());
            Assert.Equal(CountdownStatus.Running, services.Timers.Current.Timers.Single(t => t.Name == "Tea").Countdown!.Status);

            var refused = Exchange("""{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"delete_timer","arguments":{"timer":"Farm"}}}""")["result"]!;
            Assert.True(refused["isError"]!.GetValue<bool>());
            Assert.Contains("built in", refused["content"]![0]!["text"]!.GetValue<string>());

            host.Apply(false);
            var closed = Exchange("""{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_status"}}""")["result"]!;
            Assert.True(closed["isError"]!.GetValue<bool>());
            Assert.Equal(AgentPipeClient.AppNotAvailable, closed["content"]![0]!["text"]!.GetValue<string>());

            bridge.StandardInput.Close();
            Assert.True(bridge.WaitForExit(10000));
            Assert.Equal(0, bridge.ExitCode);
        }
        finally { if (!bridge.HasExited) bridge.Kill(); }
    }));

    /// <summary>Runs the dispatcher until <paramref name="task"/> finishes, since the host runs its tools on this thread.</summary>
    static T Pump<T>(Task<T> task)
    {
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }

    /// <summary>Calls from another thread while this one runs the dispatcher, which is where the host runs the tool.</summary>
    static AgentReply Call(string instance, string tool, string args = "{}")
    {
        var task = Task.Run(() => AgentPipeClient.CallAsync(AgentPipe.Name(instance), tool, JsonNode.Parse(args)!.AsObject(),
            TimeSpan.FromMilliseconds(500), CancellationToken.None));
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }

    static void WithServices(Action<AppServices> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Agent." + Guid.NewGuid());
        try
        {
            using var services = new AppServices(Application.Current, root);
            WpfTest.WithoutVoice(services);
            test(services);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
