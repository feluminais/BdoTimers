using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class ScheduleMathTests
{
    static ScheduledSpec Berlin(params Slot[] slots) => new() { TimeZoneId = "Europe/Berlin", Slots = slots };
    static Slot At(DayOfWeek day, int h, int m) => new(day, new TimeOnly(h, m));
    static DateTimeOffset Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    // 2026-09-22 is a Tuesday; Berlin is UTC+2 (CEST) until 2026-10-25.
    [Fact]
    public void Slot_later_the_same_day()
    {
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Tuesday, 14, 0)), Utc(2026, 9, 22, 10, 0), 1);
        Assert.Equal(Utc(2026, 9, 22, 12, 0), next.Single());
    }

    [Fact]
    public void Rolls_over_to_next_week()
    {
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Monday, 14, 0)), Utc(2026, 9, 22, 10, 0), 1);
        Assert.Equal(Utc(2026, 9, 28, 12, 0), next.Single());
    }

    [Fact]
    public void Slot_at_the_from_instant_is_included()
    {
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Tuesday, 14, 0)), Utc(2026, 9, 22, 12, 0), 1);
        Assert.Equal(Utc(2026, 9, 22, 12, 0), next.Single());
    }

    [Fact]
    public void Returns_requested_count_in_order()
    {
        var spec = Berlin(At(DayOfWeek.Wednesday, 10, 0), At(DayOfWeek.Tuesday, 20, 0), At(DayOfWeek.Tuesday, 14, 0));
        var next = ScheduleMath.Next(spec, Utc(2026, 9, 22, 10, 0), 3);
        Assert.Equal(new[] { Utc(2026, 9, 22, 12, 0), Utc(2026, 9, 22, 18, 0), Utc(2026, 9, 23, 8, 0) }, next);
    }

    [Fact]
    public void Spring_forward_gap_shifts_forward()
    {
        // 2026-03-29 02:30 does not exist in Berlin; it resolves to 03:30 CEST.
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Sunday, 2, 30)), Utc(2026, 3, 28, 0, 0), 1);
        Assert.Equal(Utc(2026, 3, 29, 1, 30), next.Single());
    }

    [Fact]
    public void Autumn_ambiguous_time_takes_first_instance()
    {
        // 2026-10-25 02:30 happens twice in Berlin; the first is CEST (UTC+2).
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Sunday, 2, 30)), Utc(2026, 10, 24, 0, 0), 1);
        Assert.Equal(Utc(2026, 10, 25, 0, 30), next.Single());
    }

    [Fact]
    public void Winter_time_uses_standard_offset()
    {
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Monday, 14, 0)), Utc(2026, 11, 1, 0, 0), 1);
        Assert.Equal(Utc(2026, 11, 2, 13, 0), next.Single());
    }

    [Fact]
    public void No_slots_returns_empty()
    {
        Assert.Empty(ScheduleMath.Next(Berlin(), Utc(2026, 9, 22, 10, 0), 5));
    }

    [Fact]
    public void Weekly_slot_yields_more_than_a_year_of_occurrences()
    {
        var next = ScheduleMath.Next(Berlin(At(DayOfWeek.Monday, 14, 0)), Utc(2026, 9, 22, 10, 0), 70);
        Assert.Equal(70, next.Count);
        Assert.True(next.Zip(next.Skip(1)).All(p => p.First < p.Second));
    }

    [Fact]
    public void Out_of_range_day_returns_empty_instead_of_looping()
    {
        Assert.Empty(ScheduleMath.Next(Berlin(At((DayOfWeek)9, 14, 0)), Utc(2026, 9, 22, 10, 0), 1));
    }
}
