using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

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
                Status = CountdownStatus.Running,
                EndsAtUtc = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
            },
            Alerts = new AlertConfig
            {
                LeadTimesMinutes = [5, 0],
                Overlay = new OverlayAlert { Enabled = true },
                Sound = new SoundAlert { Key = "horn.mp3" },
            },
        };
        var stopwatch = new TimerDef
        {
            Name = "Fishing",
            Kind = TimerKind.Stopwatch,
            Preset = "fishing",
            Stopwatch = new StopwatchSpec { Status = CountdownStatus.Paused, Elapsed = TimeSpan.FromMinutes(75) },
        };
        var data = new AppData
        {
            Timers = [scheduled, countdown, stopwatch],
            Muted = [new MutedOccurrence(scheduled.Id, new DateTimeOffset(2026, 9, 28, 22, 15, 0, TimeSpan.Zero))],
            SeedApplied = true,
        };

        var json = JsonSerializer.Serialize(data, JsonDefaults.Options);
        var back = JsonSerializer.Deserialize<AppData>(json, JsonDefaults.Options)!;

        Assert.True(back.SeedApplied);
        Assert.Equal(3, back.Timers.Count);
        // Alert lists compare by reference, so the alerts are checked apart from the rest.
        Assert.Equal(stopwatch, back.Timers[2] with { Alerts = stopwatch.Alerts });
        Assert.Equal(scheduled.Id, back.Timers[0].Id);
        Assert.Equal(new Slot(DayOfWeek.Monday, new TimeOnly(0, 15)), back.Timers[0].Scheduled!.Slots.Single());
        Assert.Equal(countdown.Countdown, back.Timers[1].Countdown);
        Assert.Equal(new[] { 5, 0 }, back.Timers[1].Alerts.LeadTimesMinutes);
        Assert.True(back.Timers[1].Alerts.Overlay.Enabled);
        Assert.Equal("horn.mp3", back.Timers[1].Alerts.Sound.Key);
        Assert.Null(back.Timers[0].Alerts.Sound.Key);
        Assert.Equal(data.Muted.Single(), back.Muted.Single());
        Assert.Contains("\"Monday\"", json);
    }

    [Fact]
    public void Files_without_new_fields_load()
    {
        const string timers = """
            { "timers": [ { "name": "Farm", "kind": "Countdown",
                            "countdown": { "duration": "01:00:00", "status": "Running",
                                           "endsAtUtc": "2026-09-22T12:00:00+00:00" } } ] }
            """;
        const string settings = """{ "volume": 0.5 }""";

        var data = JsonSerializer.Deserialize<AppData>(timers, JsonDefaults.Options)!;
        var s = JsonSerializer.Deserialize<AppSettings>(settings, JsonDefaults.Options)!;

        Assert.Null(data.Timers.Single().Countdown!.StartedAtUtc);
        Assert.Null(data.Timers.Single().ImageFile);
        Assert.Null(s.Window);
        Assert.Equal(BuiltInSounds.Default, s.AlertSound);
        Assert.False(s.Autostart);
        Assert.Equal(0.5f, s.Volume);
    }

    [Fact]
    public void Window_placement_round_trips()
    {
        var s = new AppSettings { Window = new WindowPlacement(10, 20, 960, 720) };
        var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s, JsonDefaults.Options), JsonDefaults.Options)!;
        Assert.Equal(s.Window, back.Window);
    }

    [Fact]
    public void Overlay_settings_round_trip()
    {
        var s = new AppSettings
        {
            OverlayLeft = 12,
            Overlay = new OverlaySettings
            {
                AlwaysShow = true,
                AlwaysShowHotkey = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x4F),
                ShowOnHotkey = true,
                ShowHotkey = new Hotkey(HotkeyModifiers.None, 0x78),
                ShowSeconds = 30,
                Layout = OverlayLayout.Card,
                Scale = 1.5,
                ShowFarm = false,
                ShowHorseRegistrations = true,
                BackgroundColor = "#3A1417",
                BackgroundImage = "bg.png",
                BackgroundOpacity = 0.4,
                TextOpacity = 0.9,
            },
        };

        var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s, JsonDefaults.Options), JsonDefaults.Options)!;

        Assert.Equal(s.Overlay, back.Overlay);
        Assert.Equal(12, back.OverlayLeft);
    }

    [Fact]
    public void Horse_preset_hotkey_and_run_number_round_trip()
    {
        var preset = Presets.CreateHorseRegistration() with
        {
            StartHotkey = new Hotkey(HotkeyModifiers.Ctrl, 0x48),
        };
        var run = preset with
        {
            Id = Guid.NewGuid(), Preset = Presets.HorseRegistrationRun,
            HorseRunNumber = 3, StartHotkey = null,
        };
        var data = new AppData { Timers = [preset, run] };

        var back = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, JsonDefaults.Options), JsonDefaults.Options)!;

        Assert.Equal(preset.StartHotkey, back.Timers[0].StartHotkey);
        Assert.Equal(3, back.Timers[1].HorseRunNumber);
        Assert.Null(back.Timers[1].StartHotkey);
    }

    [Fact]
    public void Settings_without_overlay_load_the_defaults_and_keep_the_position()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("""{ "overlayLeft": 100, "overlayTop": 40 }""", JsonDefaults.Options)!;

        Assert.Equal(new OverlaySettings(), s.Overlay);
        Assert.True(s.Overlay.Enabled);
        Assert.False(s.Overlay.AlwaysShow);
        Assert.Equal(100, s.OverlayLeft);
        Assert.Equal(40, s.OverlayTop);
    }
}
