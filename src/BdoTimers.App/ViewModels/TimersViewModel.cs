using System.Collections.ObjectModel;
using System.Windows;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class TimersViewModel
{
    readonly AppServices _services;

    public ObservableCollection<TimerRow> Rows { get; } = [];

    public TimersViewModel(AppServices services)
    {
        _services = services;
        // Changed can fire on the scheduler thread (countdown completion).
        services.Timers.Changed += () => Application.Current.Dispatcher.BeginInvoke(Reload);
        services.UiClock.Tick += now => { foreach (var row in Rows) row.RefreshRemaining(now); };
        Reload();
    }

    void Reload()
    {
        Rows.Clear();
        var now = DateTimeOffset.UtcNow;
        foreach (var timer in _services.Timers.Current.Timers
                     .OrderBy(t => t.IsBuiltIn)
                     .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var row = new TimerRow(timer, _services.Timers);
            row.RefreshRemaining(now);
            Rows.Add(row);
        }
    }

    [RelayCommand]
    void AddScheduled() => OpenEditor(new TimerDef
    {
        Name = "New timer",
        Kind = TimerKind.Scheduled,
        Scheduled = new ScheduledSpec
        {
            TimeZoneId = TimeZoneInfo.Local.Id,
            Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(20, 0))],
        },
        Alerts = _services.DefaultAlerts(),
    });

    [RelayCommand]
    void AddCountdown() => OpenEditor(new TimerDef
    {
        Name = "Farm session",
        Kind = TimerKind.Countdown,
        Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(60) },
        Alerts = _services.DefaultAlerts() with { LeadTimesMinutes = [5, 0] },
    });

    [RelayCommand]
    void Edit(TimerRow? row)
    {
        if (row is not null) OpenEditor(row.Timer);
    }

    [RelayCommand]
    void Delete(TimerRow? row)
    {
        if (row is null) return;
        var extra = row.Timer.IsBuiltIn ? "\n\nSettings → Reset boss timetable brings built-in bosses back." : "";
        if (MessageBox.Show($"Delete \"{row.Timer.Name}\"?{extra}", "Delete timer",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            _services.Timers.Delete(row.Timer.Id);
    }

    void OpenEditor(TimerDef timer) =>
        new TimerEditorWindow(new TimerEditorViewModel(timer, _services.Timers))
        {
            Owner = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault(),
        }.ShowDialog();
}
