using BdoTimers.Core.Model;
using BdoTimers.Core.Text;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class SpeechLinesTests
{
    [Fact]
    public void IncludesEveryLeadAndNowForNamesAndSlotLabels()
    {
        var timer = new TimerDef { Name = "Guild war", Scheduled = new() { Slots = [new(DayOfWeek.Monday, new(12, 0), "Battle")] } };
        var lines = SpeechLines.ForTimers([timer], [5, 1, 0], new Clock());
        Assert.Contains("Guild war, Battle in 5 minutes", lines);
        Assert.Contains("Guild war, Battle in 1 minute", lines);
        Assert.Contains("Guild war, Battle now", lines);
    }

    [Fact]
    public void SharedSpawnsIncludeSilentNamesAndUseFirstSpeakingTemplate()
    {
        var schedule = new ScheduledSpec { Slots = [new(DayOfWeek.Monday, new(12, 0))] };
        var a = new TimerDef { Name = "A", Scheduled = schedule, Alerts = new() { Tts = new() { Enabled = false } } };
        var b = new TimerDef { Name = "B", Scheduled = schedule, Alerts = new() { Tts = new() { Template = "{name}: {minutes}" } } };
        Assert.Contains("A and B: 5", SpeechLines.ForTimers([a, b], [5, 0], new Clock()));
        Assert.DoesNotContain("A in 5 minutes", SpeechLines.ForTimers([a, b], [5, 0], new Clock()));
    }

    [Fact]
    public void SharedSpawnsUseUtcAndIncludeWeeksWhenAnAlternatingTimerIsAbsent()
    {
        var schedule = new ScheduledSpec { TimeZoneId = "Europe/Berlin", Slots = [new(DayOfWeek.Monday, new(12, 0))] };
        var a = new TimerDef { Name = "A", Scheduled = schedule };
        var b = new TimerDef { Name = "B", Scheduled = schedule with { TimeZoneId = "W. Europe Standard Time" } };
        var c = new TimerDef { Name = "C", Scheduled = schedule with { EveryWeeks = 2, WeekAnchor = new(2026, 10, 5) } };
        var lines = SpeechLines.ForTimers([a, b, c], [5, 0], new Clock());
        Assert.Contains("A and B in 5 minutes", lines);
        Assert.Contains("A, B and C now", lines);
    }

    [Fact]
    public void InvalidPeerScheduleDoesNotBlockHealthyTimerLines()
    {
        var good = new TimerDef { Name = "Good", Scheduled = new() { Slots = [new(DayOfWeek.Monday, new(12, 0))] } };
        var bad = new TimerDef { Name = "Bad", Scheduled = new() { TimeZoneId = "missing-zone", Slots = [new(DayOfWeek.Monday, new(12, 0))] } };
        Assert.Contains("Good now", SpeechLines.ForTimers([good, bad], [0], new Clock(), good.Id));
    }

    sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero); }
}
