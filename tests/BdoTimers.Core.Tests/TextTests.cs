using System.Globalization;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using static BdoTimers.Core.Tests.TestTimes;

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

        var soon = AlertMessage.Build(new AlertEvent([timer], T0, 5, 5));
        Assert.Equal("Nouver", soon.Title);
        Assert.Equal("Nouver in 5 minutes", soon.Speech);
        Assert.StartsWith("In 5 min", soon.Body);

        var now = AlertMessage.Build(new AlertEvent([timer], T0, 0, 0));
        Assert.Equal("Nouver now", now.Speech);
        Assert.StartsWith("Now", now.Body);
    }

    [Fact]
    public void Shared_spawn_names_every_boss_once()
    {
        TimerDef[] two = [new() { Name = "Kzarka" }, new() { Name = "Uturi" }];
        TimerDef[] three = [new() { Name = "Kzarka" }, new() { Name = "Nouver" }, new() { Name = "Uturi" }];

        var soon = AlertMessage.Build(new AlertEvent(two, T0, 5, 5));
        var now = AlertMessage.Build(new AlertEvent(three, T0, 0, 0));

        Assert.Equal("Kzarka · Uturi", soon.Title);
        Assert.Equal("Kzarka and Uturi in 5 minutes", soon.Speech);
        Assert.Equal("Kzarka, Nouver and Uturi now", now.Speech);
    }

    [Fact]
    public void Shared_spawn_speaks_with_the_first_voice_enabled_template()
    {
        var quiet = new TimerDef { Name = "Kzarka", Alerts = new AlertConfig { Tts = new TtsAlert { Enabled = false, Template = "unused" } } };
        var loud = new TimerDef { Name = "Uturi", Alerts = new AlertConfig { Tts = new TtsAlert { Template = "{name} soon" } } };

        Assert.Equal("Kzarka and Uturi soon", AlertMessage.Build(new AlertEvent([quiet, loud], T0, 5, 5)).Speech);
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
    [InlineData(-5, "−00:00:05")]
    [InlineData(0, "00:00:00")]
    [InlineData(5, "00:00:05")]
    public void Formats_signed_clock(int seconds, string expected) =>
        Assert.Equal(expected, DurationFormat.SignedClock(TimeSpan.FromSeconds(seconds)));

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
    [InlineData("30:15")]
    [InlineData("1:60")]
    [InlineData("71582789:00")]
    [InlineData("2147483647:59")]
    [InlineData("abc")]
    [InlineData("")]
    public void Rejects_bad_durations(string text) => Assert.False(Parsing.TryParseDuration(text, out _));

    [Fact]
    public void Formats_durations() => Assert.Equal("1:30", Parsing.FormatDuration(TimeSpan.FromMinutes(90)));

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

    [Theory]
    [InlineData("2200", "22:00")]
    [InlineData("220", "22:0")]
    [InlineData("0930", "09:30")]
    [InlineData("930", "9:30")]
    [InlineData("93", "9:3")]
    [InlineData("22", "22")]
    [InlineData("9", "9")]
    [InlineData("", "")]
    [InlineData("22:00", "22:00")]
    [InlineData("9:3", "9:3")]
    [InlineData("2a00", "2a00")]
    [InlineData("9 ", "09:")]
    [InlineData("09 ", "09:")]
    [InlineData("13.", "13:")]
    [InlineData("0 ", "00:")]
    [InlineData("25 ", "25 ")]
    [InlineData(" ", " ")]
    [InlineData("9  ", "9  ")]
    [InlineData("123 ", "123 ")]
    public void Adds_the_colon_typed_digits_imply(string text, string expected) =>
        Assert.Equal(expected, TimeEntry.AddColon(text));

    [Theory]
    [InlineData("9", "09:00")]
    [InlineData("09", "09:00")]
    [InlineData("22", "22:00")]
    [InlineData("14", "14:00")]
    [InlineData("24", "02:40")]
    [InlineData("97", "97")]
    [InlineData("0", "00:00")]
    [InlineData("13:", "13:00")]
    [InlineData("134", "13:40")]
    [InlineData("13:4", "13:40")]
    [InlineData("93", "09:30")]
    [InlineData("9:30", "09:30")]
    [InlineData("2200", "22:00")]
    [InlineData(" 9 ", "09:00")]
    [InlineData("1 2", "01:20")]
    [InlineData("13 40", "13:40")]
    [InlineData("13.40", "13:40")]
    [InlineData("9.3", "09:30")]
    [InlineData("13 ", "13:00")]
    [InlineData("1  2", "1  2")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("22:00", "22:00")]
    [InlineData("", "")]
    [InlineData("25:00", "25:00")]
    [InlineData("2500", "2500")]
    [InlineData("1:7", "1:7")]
    [InlineData("2a", "2a")]
    [InlineData("12:345", "12:345")]
    public void Finishes_a_typed_time(string text, string expected) =>
        Assert.Equal(expected, TimeEntry.Finish(text));

    [Fact]
    public void Parses_minutes_in_range()
    {
        Assert.True(Parsing.TryParseMinutes("90", 1, 1440, out var v));
        Assert.Equal(90, v);
        Assert.False(Parsing.TryParseMinutes("0", 1, 1440, out _));
        Assert.False(Parsing.TryParseMinutes("x", 1, 1440, out _));
    }

    static readonly string[] Zones =
    [
        "(UTC+09:00) Osaka, Sapporo, Tokyo",
        "(UTC+01:00) Sarajevo, Skopje, Warsaw, Zagreb",
        "(UTC+02:00) Helsinki, Kyiv, Riga, Sofia, Tallinn, Vilnius",
        "(UTC+02:00) Kaliningrad",
        "(UTC) Coordinated Universal Time",
        "(UTC+00:00) São Tomé",
    ];

    [Theory]
    [InlineData("kyiv", 2)]
    [InlineData("ky", 2)]
    [InlineData("KA", 3)]
    [InlineData("sa", 0)]
    [InlineData("rsaw", 1)]
    [InlineData("(utc)", 4)]
    [InlineData("+09", 0)]
    [InlineData("sao tome", 5)]
    public void List_search_prefers_a_prefix_then_a_word_start_then_any_match(string query, int expected) =>
        Assert.Equal(expected, ListSearch.Find(Zones, query));

    [Theory]
    [InlineData("")]
    [InlineData("berlin")]
    public void List_search_finds_nothing_for_no_match(string query) => Assert.Equal(-1, ListSearch.Find(Zones, query));
}
