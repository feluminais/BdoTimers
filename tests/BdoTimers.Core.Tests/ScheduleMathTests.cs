using System.Globalization;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class ScheduleMathTests
{
    static ScheduledSpec Berlin(params Slot[] slots) => new() { TimeZoneId = "Europe/Berlin", Slots = slots };
    static Slot At(DayOfWeek day, int h, int m) => new(day, new TimeOnly(h, m));
    static DateTimeOffset Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    // 2026-09-22 is a Tuesday; Berlin is UTC+2 (CEST) until 2026-10-25.
    [Theory]
    [InlineData(DayOfWeek.Tuesday, 14, 0, "2026-09-22 10:00", "2026-09-22 12:00")] // later the same day
    [InlineData(DayOfWeek.Monday, 14, 0, "2026-09-22 10:00", "2026-09-28 12:00")] // rolls over to next week
    [InlineData(DayOfWeek.Tuesday, 14, 0, "2026-09-22 12:00", "2026-09-22 12:00")] // a slot at the from instant
    [InlineData(DayOfWeek.Sunday, 2, 30, "2026-03-28 00:00", "2026-03-29 01:30")] // 02:30 is in the spring-forward gap: 03:30 CEST
    [InlineData(DayOfWeek.Sunday, 2, 30, "2026-10-24 00:00", "2026-10-25 00:30")] // 02:30 happens twice: the first, CEST
    [InlineData(DayOfWeek.Monday, 14, 0, "2026-11-01 00:00", "2026-11-02 13:00")] // winter time uses the standard offset
    public void First_occurrence(DayOfWeek day, int hour, int minute, string fromUtc, string expectedUtc) =>
        Assert.Equal(ParseUtc(expectedUtc), ScheduleMath.From(Berlin(At(day, hour, minute)), ParseUtc(fromUtc)).First());

    static DateTimeOffset ParseUtc(string text) =>
        DateTimeOffset.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    [Fact]
    public void Occurrences_come_in_order()
    {
        var spec = Berlin(At(DayOfWeek.Wednesday, 10, 0), At(DayOfWeek.Tuesday, 20, 0), At(DayOfWeek.Tuesday, 14, 0));
        var next = ScheduleMath.From(spec, Utc(2026, 9, 22, 10, 0)).Take(3);
        Assert.Equal(new[] { Utc(2026, 9, 22, 12, 0), Utc(2026, 9, 22, 18, 0), Utc(2026, 9, 23, 8, 0) }, next);
    }

    [Fact]
    public void No_slots_returns_empty()
    {
        Assert.Empty(ScheduleMath.From(Berlin(), Utc(2026, 9, 22, 10, 0)));
    }

    [Fact]
    public void Weekly_slot_yields_more_than_a_year_of_occurrences()
    {
        var next = ScheduleMath.From(Berlin(At(DayOfWeek.Monday, 14, 0)), Utc(2026, 9, 22, 10, 0)).Take(70).ToList();
        Assert.Equal(70, next.Count);
        Assert.True(next.Zip(next.Skip(1)).All(p => p.First < p.Second));
    }

    [Fact]
    public void Out_of_range_day_returns_empty_instead_of_looping()
    {
        Assert.Empty(ScheduleMath.From(Berlin(At((DayOfWeek)9, 14, 0)), Utc(2026, 9, 22, 10, 0)));
    }

    // 2026-09-25 12:00 UTC is 15:00 in Kyiv.
    [Theory]
    [InlineData("2026-09-25 12:00", 14, 20, "2026-09-25 11:20")] // earlier today is today
    [InlineData("2026-09-25 22:00", 23, 30, "2026-09-25 20:30")] // still ahead at 01:00 on the 26th is yesterday
    [InlineData("2026-09-25 12:00", 15, 0, "2026-09-25 12:00")] // the current minute is now
    public void Most_recent_clock_time(string nowUtc, int hour, int minute, string expectedUtc) =>
        Assert.Equal(ParseUtc(expectedUtc),
            ScheduleMath.MostRecent(new TimeOnly(hour, minute), ParseUtc(nowUtc), TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv")));
}
