using System.Globalization;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;

namespace BdoTimers.Core.Tests;

public class TextTests
{
    [Theory]
    [InlineData(1, "1 minute")]
    [InlineData(5, "5 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(125, "2 hours 5 minutes")]
    public void Humanizes_minutes(int minutes, string expected) =>
        Assert.Equal(expected, AlertMessage.Humanize(minutes));

    [Fact]
    public void Fills_template_placeholders()
    {
        Assert.Equal("Kzarka in 5 minutes (5)", AlertMessage.Fill("{name} in {duration} ({minutes})", "Kzarka", 5));
    }

    [Fact]
    public void Builds_upcoming_and_now_messages()
    {
        var timer = new TimerDef { Name = "Nouver" };
        var at = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

        var soon = AlertMessage.Build(new AlertEvent(timer, at, 5, 5));
        Assert.Equal("Nouver", soon.Title);
        Assert.Equal("Nouver in 5 minutes", soon.Speech);
        Assert.StartsWith("In 5 min", soon.Body);

        var now = AlertMessage.Build(new AlertEvent(timer, at, 0, 0));
        Assert.Equal("Nouver now", now.Speech);
        Assert.StartsWith("Now", now.Body);
    }

    [Theory]
    [InlineData(-5, "now")]
    [InlineData(249, "04:09")]
    [InlineData(3900, "1h 05m")]
    [InlineData(97200, "1d 03h")]
    public void Formats_countdowns(int seconds, string expected) =>
        Assert.Equal(expected, DurationFormat.Countdown(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(-5, "00:00:00")]
    [InlineData(14195, "03:56:35")]
    [InlineData(93600, "1d 02:00:00")]
    public void Formats_clock(int seconds, string expected) =>
        Assert.Equal(expected, DurationFormat.Clock(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData("1:30", 90)]
    [InlineData("0:05", 5)]
    [InlineData(" 90 ", 90)]
    [InlineData("24:00", 1440)]
    public void Parses_durations(string text, int minutes)
    {
        Assert.True(Parsing.TryParseDuration(text, out var d));
        Assert.Equal(TimeSpan.FromMinutes(minutes), d);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0:00")]
    [InlineData("24:01")]
    [InlineData("1:60")]
    [InlineData("abc")]
    [InlineData("")]
    public void Rejects_bad_durations(string text) => Assert.False(Parsing.TryParseDuration(text, out _));

    [Fact]
    public void Formats_durations() => Assert.Equal("1:30", Parsing.FormatDuration(TimeSpan.FromMinutes(90)));

    [Fact]
    public void Parses_lead_times()
    {
        Assert.True(Parsing.TryParseLeadTimes(" 5, 15;0 1 5", out var leads, out _));
        Assert.Equal(new[] { 15, 5, 1, 0 }, leads);
        Assert.Equal("15, 5, 1, 0", Parsing.FormatLeadTimes(leads));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("2000")]
    public void Rejects_bad_lead_times(string text)
    {
        Assert.False(Parsing.TryParseLeadTimes(text, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("21:15", 21, 15)]
    [InlineData("0:05", 0, 5)]
    [InlineData(" 09:00 ", 9, 0)]
    public void Parses_times(string text, int h, int m)
    {
        Assert.True(Parsing.TryParseTime(text, out var t));
        Assert.Equal(new TimeOnly(h, m), t);
    }

    [Fact]
    public void Formatted_time_parses_back_in_any_culture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");
            var text = Parsing.FormatTime(new TimeOnly(21, 15));
            Assert.Equal("21:15", text);
            Assert.True(Parsing.TryParseTime(text, out var back));
            Assert.Equal(new TimeOnly(21, 15), back);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("9")]
    [InlineData("")]
    public void Rejects_bad_times(string text) => Assert.False(Parsing.TryParseTime(text, out _));

    [Fact]
    public void Parses_minutes_in_range()
    {
        Assert.True(Parsing.TryParseMinutes("90", 1, 1440, out var v));
        Assert.Equal(90, v);
        Assert.False(Parsing.TryParseMinutes("0", 1, 1440, out _));
        Assert.False(Parsing.TryParseMinutes("x", 1, 1440, out _));
    }
}
