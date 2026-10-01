using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class UpcomingQueryTests
{

    [Fact]
    public void Overlay_shows_only_opted_in_unmuted_items_inside_their_window()
    {
        var overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 10 };
        var soon = TestTimers.Countdown(BerlinNoon.AddMinutes(8), 0) with { Name = "Soon" };
        soon = soon with { Alerts = soon.Alerts with { Overlay = overlay } };
        var later = TestTimers.Countdown(BerlinNoon.AddMinutes(12), 0) with { Name = "Later" };
        later = later with { Alerts = later.Alerts with { Overlay = overlay } };
        var notOptedIn = TestTimers.Countdown(BerlinNoon.AddMinutes(3), 0) with { Name = "Plain" };
        var muted = TestTimers.Countdown(BerlinNoon.AddMinutes(4), 0) with { Name = "Muted" };
        muted = muted with { Alerts = muted.Alerts with { Overlay = overlay } };
        var data = new AppData
        {
            Timers = [soon, later, notOptedIn, muted],
            Muted = [new MutedOccurrence(muted.Id, BerlinNoon.AddMinutes(4))],
        };

        var items = UpcomingQuery.ForOverlay(data, BerlinNoon);

        Assert.Equal(new[] { "Soon" }, items.Select(i => i.Timer.Name));
    }
}
