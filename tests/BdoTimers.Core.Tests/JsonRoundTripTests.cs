using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class JsonRoundTripTests
{
    [Fact]
    public void AppData_survives_serialization()
    {
        var scheduled = new TimerDef
        {
            Name = "Kzarka",
            Kind = TimerKind.Scheduled,
            IsBuiltIn = true,
            Scheduled = new ScheduledSpec
            {
                TimeZoneId = "Europe/Berlin",
                Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(0, 15))],
            },
        };
        var countdown = new TimerDef
        {
            Name = "Farm",
            Kind = TimerKind.Countdown,
            Countdown = new CountdownSpec
            {
                Duration = TimeSpan.FromMinutes(90),
                AutoRepeat = true,
                Status = CountdownStatus.Running,
                EndsAtUtc = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
            },
            Alerts = new AlertConfig { LeadTimesMinutes = [5, 0], Overlay = new OverlayAlert { Enabled = true } },
        };
        var data = new AppData
        {
            Timers = [scheduled, countdown],
            Muted = [new MutedOccurrence(scheduled.Id, new DateTimeOffset(2026, 9, 28, 22, 15, 0, TimeSpan.Zero))],
            SeedApplied = true,
        };

        var json = JsonSerializer.Serialize(data, JsonDefaults.Options);
        var back = JsonSerializer.Deserialize<AppData>(json, JsonDefaults.Options)!;

        Assert.True(back.SeedApplied);
        Assert.Equal(2, back.Timers.Count);
        Assert.Equal(scheduled.Id, back.Timers[0].Id);
        Assert.Equal(new Slot(DayOfWeek.Monday, new TimeOnly(0, 15)), back.Timers[0].Scheduled!.Slots.Single());
        Assert.Equal(countdown.Countdown, back.Timers[1].Countdown);
        Assert.Equal(new[] { 5, 0 }, back.Timers[1].Alerts.LeadTimesMinutes);
        Assert.True(back.Timers[1].Alerts.Overlay.Enabled);
        Assert.Equal(data.Muted.Single(), back.Muted.Single());
        Assert.Contains("\"Monday\"", json);
    }
}
