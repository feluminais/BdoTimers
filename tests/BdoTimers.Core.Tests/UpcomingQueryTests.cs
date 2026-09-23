using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class UpcomingQueryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero); // Tue 12:00 Berlin

    [Fact]
    public void Lists_enabled_timers_in_time_order_with_mute_flags()
    {
        var karanda = TestTimers.Scheduled("Karanda", DayOfWeek.Tuesday, 20, 0, 0);
        var kzarka = TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0, 0);
        var off = TestTimers.Scheduled("Off", DayOfWeek.Tuesday, 13, 0, 0) with { Enabled = false };
        var farm = TestTimers.Countdown(Now.AddMinutes(30), 0);
        var data = new AppData
        {
            Timers = [karanda, kzarka, off, farm],
            Muted = [new MutedOccurrence(kzarka.Id, Now.AddHours(2))],
        };

        var items = UpcomingQuery.Next(data, Now, TimeSpan.FromHours(12), 10);

        Assert.Equal(new[] { "Farm", "Kzarka", "Karanda" }, items.Select(i => i.Timer.Name));
        Assert.True(items[1].Muted);
        Assert.False(items[0].Muted);
    }

    [Fact]
    public void Respects_max()
    {
        var kzarka = TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0, 0);
        var items = UpcomingQuery.Next(new AppData { Timers = [kzarka] }, Now, TimeSpan.FromDays(30), 2);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Overlay_shows_only_opted_in_unmuted_items_inside_their_window()
    {
        var overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 10 };
        var soon = TestTimers.Countdown(Now.AddMinutes(8), 0) with { Name = "Soon" };
        soon = soon with { Alerts = soon.Alerts with { Overlay = overlay } };
        var later = TestTimers.Countdown(Now.AddMinutes(12), 0) with { Name = "Later" };
        later = later with { Alerts = later.Alerts with { Overlay = overlay } };
        var notOptedIn = TestTimers.Countdown(Now.AddMinutes(3), 0) with { Name = "Plain" };
        var muted = TestTimers.Countdown(Now.AddMinutes(4), 0) with { Name = "Muted" };
        muted = muted with { Alerts = muted.Alerts with { Overlay = overlay } };
        var data = new AppData
        {
            Timers = [soon, later, notOptedIn, muted],
            Muted = [new MutedOccurrence(muted.Id, Now.AddMinutes(4))],
        };

        var items = UpcomingQuery.ForOverlay(data, Now);

        Assert.Equal(new[] { "Soon" }, items.Select(i => i.Timer.Name));
    }
}
