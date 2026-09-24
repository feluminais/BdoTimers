using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;

namespace BdoTimers.App;

public sealed class TrayIcon : IDisposable
{
    readonly TaskbarIcon _icon;

    public TrayIcon(AppServices services)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "BDO Timers",
            NoLeftClickDelay = true,
            IconSource = new GeneratedIconSource
            {
                Text = "B",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xC9, 0x8A)),
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0B, 0x0C)),
            },
            ContextMenu = BuildMenu(services),
        };
        _icon.TrayLeftMouseUp += (_, _) => services.ShowMainWindow();
        // Efficiency mode would lower process priority while hidden and delay alerts.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    static ContextMenu BuildMenu(AppServices s)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Open", s.ShowMainWindow));
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

    public void Dispose() => _icon.Dispose();
}
