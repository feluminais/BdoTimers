using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class CustomOverlayTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    static readonly OverlaySettings OnlyCustom = new()
    {
        ShowClock = false, ShowPrevious = false, ShowNext = false, ShowFarm = false, ShowFishing = false,
        ShowHorseRegistrations = false,
    };

    static TimerDef Custom(string name, CountdownSpec countdown) => new()
    {
        Name = name, Kind = TimerKind.Countdown, Countdown = countdown, Enabled = false,
    };

    [Fact]
    public void Running_and_paused_custom_countdowns_show_in_saved_order_even_with_alerts_off()
    {
        var first = Custom("Z first", CountdownOps.Start(new CountdownSpec(), Now));
        var second = Custom("A second", CountdownOps.Pause(CountdownOps.Start(new CountdownSpec(), Now.AddMinutes(-10)), Now));
        var idle = Custom("Ready", new CountdownSpec());
        var data = new AppData { Timers = [first, .. Presets.Create(), idle, second, new TimerDef { Kind = TimerKind.Scheduled }] };
        var content = OverlayContent.Build(data, OnlyCustom, Now.AddMinutes(5));
        Assert.Equal([first.Id, second.Id], content.CustomTimers.Select(t => t.Id));
        Assert.Equal(["Z first", "A second"], content.CustomTimers.Select(t => t.Name));
        Assert.Equal(TimeSpan.FromMinutes(55), content.CustomTimers[0].Time);
        Assert.False(content.CustomTimers[0].Paused);
        Assert.Equal(TimeSpan.FromMinutes(50), content.CustomTimers[1].Time);
        Assert.True(content.CustomTimers[1].Paused);
        Assert.False(content.IsEmpty);
        Assert.True(new OverlayPresence().IsVisible(OnlyCustom with { AlwaysShow = true }, Now, content, false));
        Assert.Equal([first.Id, second.Id], OverlayContent.Build(data, OnlyCustom, Now.AddMinutes(6)).CustomTimers.Select(t => t.Id));
        var renamedAndResumed = second with
        {
            Name = "Renamed", Countdown = CountdownOps.Resume(second.Countdown!, Now.AddMinutes(5)),
        };
        var updated = data with { Timers = data.Timers.Select(t => t.Id == second.Id ? renamedAndResumed : t).ToList() };
        Assert.Equal([first.Id, second.Id], OverlayContent.Build(updated, OnlyCustom, Now.AddMinutes(6)).CustomTimers.Select(t => t.Id));
        Assert.True(OverlayContent.Build(data, OnlyCustom with { ShowCustomTimers = false }, Now).IsEmpty);
    }

    [Fact]
    public void Running_and_paused_custom_stopwatches_show_what_they_have_counted_beside_the_Fishing_row()
    {
        TimerDef Stopwatch(string name, StopwatchSpec spec) => new() { Name = name, Kind = TimerKind.Stopwatch, Stopwatch = spec };
        var running = Stopwatch("Grinding", StopwatchOps.Start(new StopwatchSpec(), Now.AddMinutes(-30)));
        var paused = Stopwatch("Break", StopwatchOps.Pause(StopwatchOps.Start(new StopwatchSpec(), Now.AddMinutes(-20)), Now.AddMinutes(-5)));
        var idle = Stopwatch("Ready", new StopwatchSpec());
        var fishing = Presets.Create().Single(t => t.Preset == Presets.Fishing) with { Stopwatch = StopwatchOps.Start(new StopwatchSpec(), Now.AddMinutes(-10)) };
        var data = new AppData { Timers = [running, idle, paused, fishing] };

        var content = OverlayContent.Build(data, OnlyCustom with { ShowFishing = true }, Now);

        Assert.Equal(["Grinding", "Break"], content.CustomTimers.Select(t => t.Name));
        Assert.Equal([TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(15)], content.CustomTimers.Select(t => t.Time));
        Assert.Equal([false, true], content.CustomTimers.Select(t => t.Paused));
        Assert.Equal(TimeSpan.FromMinutes(10), content.FishingElapsed);
        Assert.Empty(OverlayContent.Build(data, OnlyCustom with { ShowCustomTimers = false }, Now).CustomTimers);
    }

    [Fact]
    public void Custom_rows_deduplicate_popups_without_losing_popup_visibility_and_return_when_section_is_off()
    {
        var timer = Custom("Grind", CountdownOps.Start(new CountdownSpec { Duration = TimeSpan.FromMinutes(3) }, Now)) with
        {
            Enabled = true, Alerts = new AlertConfig { Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 5 } },
        };
        var data = new AppData { Timers = [timer] };
        var content = OverlayContent.Build(data, OnlyCustom, Now);
        Assert.Empty(content.PopUps);
        Assert.True(content.HasDuePopUp);
        Assert.True(new OverlayPresence().IsVisible(OnlyCustom, Now, content, false));
        Assert.Single(OverlayContent.Build(data, OnlyCustom with { ShowCustomTimers = false }, Now).PopUps);
        Assert.Empty(OverlayContent.Build(data, OnlyCustom, Now.AddMinutes(4)).CustomTimers);
    }

    [Fact]
    public void New_fields_default_safely_for_old_files_and_section_choice_round_trips()
    {
        var old = JsonSerializer.Deserialize<TimerDef>("""{"kind":"Countdown","countdown":{}}""", JsonDefaults.Options)!;
        Assert.Null(old.ControlHotkey);
        Assert.True(JsonSerializer.Deserialize<OverlaySettings>("{}", JsonDefaults.Options)!.ShowCustomTimers);
        var settings = OnlyCustom with { ShowCustomTimers = false };
        Assert.False(JsonSerializer.Deserialize<OverlaySettings>(JsonSerializer.Serialize(settings, JsonDefaults.Options), JsonDefaults.Options)!.ShowCustomTimers);
    }
}
