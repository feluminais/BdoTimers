using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BdoTimers.Core.Diagnostics;
using H.NotifyIcon;

namespace BdoTimers.App;

/// <summary>
/// The notification area icon. Creating it fails while the taskbar isn't ready, e.g. at logon autostart; it is then
/// retried until it works while the rest of the app runs.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    readonly TaskbarIcon _icon;
    readonly RepeatingErrorLog _errors;
    readonly DispatcherTimer _retry = new() { Interval = TimeSpan.FromSeconds(5) };

    public TrayIcon(AppServices services)
    {
        _errors = new RepeatingErrorLog("Tray icon", services.Clock);
        _icon = new TaskbarIcon
        {
            ToolTipText = "BDO Timers",
            NoLeftClickDelay = true,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = BuildMenu(services),
        };
        _icon.TrayLeftMouseUp += (_, _) => services.ShowMainWindow();
        _retry.Tick += (_, _) => { if (TryCreate()) _retry.Stop(); };
        if (!TryCreate()) _retry.Start();
    }

    bool TryCreate()
    {
        try
        {
            // Efficiency mode would lower process priority while hidden and delay alerts.
            _icon.ForceCreate(enablesEfficiencyMode: false);
            _errors.Succeeded();
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _errors.Failed(ex);
            return false;
        }
    }

    static ContextMenu BuildMenu(AppServices s)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Open", s.ShowMainWindow));
        menu.Items.Add(Item("Overlay settings", s.ShowOverlaySettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Pause alerts for 1 hour", () => s.PauseAlerts(TimeSpan.FromHours(1))));
        menu.Items.Add(Item("Pause alerts until resumed", () => s.PauseAlerts(null)));
        menu.Items.Add(Item("Resume alerts", s.ResumeAlerts));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Quit", s.Quit));
        return menu;
    }

    static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    public void Dispose()
    {
        _retry.Stop();
        _icon.Dispose();
    }
}
