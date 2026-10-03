using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class AlertGroupingTests
{
    static readonly DateTimeOffset T = new(2026, 9, 22, 17, 0, 0, TimeSpan.Zero);

    static AlertEvent Event(string name, DateTimeOffset at, int minutesLeft) =>
        new([new TimerDef { Name = name, IsBuiltIn = true }], at, minutesLeft, minutesLeft);

    [Fact]
    public void Same_instant_and_minutes_left_merge_with_Morning_Light_first()
    {
        var merged = AlertGrouping.Group([Event("Uturi", T, 5), Event("Kzarka", T, 5)]);

        Assert.Equal(new[] { "Uturi", "Kzarka" }, merged.Single().Timers.Select(t => t.Name));
        Assert.Equal(T, merged.Single().OccurrenceUtc);
    }

    [Fact]
    public void Different_instants_or_minutes_left_stay_separate()
    {
        var merged = AlertGrouping.Group([Event("Kzarka", T, 5), Event("Nouver", T.AddMinutes(30), 5), Event("Uturi", T, 1)]);

        Assert.Equal(3, merged.Count);
    }

    [Fact]
    public void A_single_alert_is_unchanged()
    {
        var alert = Event("Kzarka", T, 5);
        Assert.Same(alert, AlertGrouping.Group([alert]).Single());
    }
}
