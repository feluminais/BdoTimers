using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class OccurrenceSourceTests
{
    // 2026-09-22 is a Tuesday; Berlin is UTC+2 until 2026-10-25.
    static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Next_is_the_first_occurrence_from_now_within_a_week()
    {
        Assert.Equal(T0.AddDays(6), OccurrenceSource.Next(TestTimers.Scheduled("Kzarka", DayOfWeek.Monday, 14, 0), T0));
        Assert.Equal(T0, OccurrenceSource.Next(TestTimers.Scheduled("Kzarka", DayOfWeek.Tuesday, 14, 0), T0));
        Assert.Equal(T0.AddMinutes(5), OccurrenceSource.Next(TestTimers.Countdown(T0.AddMinutes(5)), T0));
    }

    [Fact]
    public void Next_is_null_without_an_occurrence()
    {
        Assert.Null(OccurrenceSource.Next(TestTimers.Countdown(T0.AddMinutes(-1)), T0));
        Assert.Null(OccurrenceSource.Next(new TimerDef { Kind = TimerKind.Scheduled, Scheduled = new ScheduledSpec() }, T0));
    }
}
