using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class UpcomingQueryTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero); // Tue 12:00 Berlin

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
