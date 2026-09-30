using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class DataMigrationsTests
{
    static TimerDef With(string name, IReadOnlyList<int>? leads) =>
        new() { Name = name, Alerts = new AlertConfig { LeadTimesMinutes = leads } };

    [Fact]
    public void Timers_holding_a_default_or_an_app_assigned_list_follow_the_default()
    {
        var data = new AppData
        {
            Timers = [With("Default now", [0, 1]), With("Old default", [15, 5, 1, 0]), With("Countdown", [5, 0]),
                      With("Own", [30, 10]), With("Already following", null)],
        };

        var migrated = DataMigrations.Apply(data, new AppSettings { DefaultLeadTimesMinutes = [1, 0] });

        var leads = migrated.Timers.ToDictionary(t => t.Name, t => t.Alerts.LeadTimesMinutes);
        Assert.Null(leads["Default now"]);
        Assert.Null(leads["Old default"]);
        Assert.Null(leads["Countdown"]);
        Assert.Equal([30, 10], leads["Own"]!);
        Assert.Null(leads["Already following"]);
        Assert.Equal(DataMigrations.Current, migrated.DataVersion);
    }

    [Fact]
    public void Timers_with_a_retired_built_in_sound_go_back_to_default()
    {
        static TimerDef Sound(string name, string? key) => new() { Name = name, Alerts = new AlertConfig { Sound = new SoundAlert { Key = key } } };
        var data = new AppData { DataVersion = 2, Timers = [Sound("Retired", "horn"), Sound("Current", "ping"), Sound("Mine", "mine.mp3")] };

        var keys = DataMigrations.Apply(data, new AppSettings()).Timers.ToDictionary(t => t.Name, t => t.Alerts.Sound.Key);

        Assert.Null(keys["Retired"]);
        Assert.Equal("ping", keys["Current"]);
        Assert.Equal("mine.mp3", keys["Mine"]);
    }

    [Fact]
    public void Current_data_is_left_alone()
    {
        var data = new AppData { DataVersion = DataMigrations.Current, Timers = [With("Mine", [15, 5, 1, 0])] };

        Assert.Same(data, DataMigrations.Apply(data, new AppSettings()));
    }

    [Fact]
    public void Horse_registration_is_added_after_the_existing_timers()
    {
        var data = new AppData { DataVersion = 3, Timers = [With("Mine", null)] };

        var migrated = DataMigrations.Apply(data, new AppSettings());

        Assert.Equal(["Mine", "Horse registration"], migrated.Timers.Select(t => t.Name));
        Assert.Equal(Presets.HorseRegistration, migrated.Timers[1].Preset);
    }

    [Fact]
    public void A_deleted_horse_registration_is_not_added_back()
    {
        var settings = new AppSettings();
        var first = Presets.Ensure(DataMigrations.Apply(new AppData(), settings));
        var deleted = first with { Timers = first.Timers.Where(t => t.Preset != Presets.HorseRegistration).ToList() };

        var restarted = Presets.Ensure(DataMigrations.Apply(deleted, settings));

        Assert.Single(first.Timers, t => t.Preset == Presets.HorseRegistration);
        Assert.DoesNotContain(restarted.Timers, t => t.Preset == Presets.HorseRegistration);
    }

    [Fact]
    public void Timers_without_their_own_times_use_the_default()
    {
        var own = new AlertConfig { LeadTimesMinutes = [30] };
        var follows = new AlertConfig();

        Assert.Equal([30], own.LeadTimes([1, 0]));
        Assert.Equal([1, 0], follows.LeadTimes([1, 0]));
        Assert.True(own.OverridesDefaults);
        Assert.False(follows.OverridesDefaults);
        Assert.True((follows with { Sound = new SoundAlert { Key = "harp" } }).OverridesDefaults);
        Assert.True((follows with { Sound = new SoundAlert { Enabled = false } }).OverridesDefaults);
    }

    [Fact]
    public void Running_horse_preset_becomes_a_separate_run()
    {
        var now = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var template = Presets.CreateHorseRegistration();
        template = template with { Countdown = CountdownOps.Start(template.Countdown!, now) };
        var data = new AppData { DataVersion = 4, Timers = [template] };

        var migrated = DataMigrations.Apply(data, new AppSettings());

        Assert.Equal(CountdownStatus.Idle, migrated.Timers[0].Countdown!.Status);
        var run = migrated.Timers[1];
        Assert.Equal(Presets.HorseRegistrationRun, run.Preset);
        Assert.Equal(now.AddMinutes(10), run.Countdown!.EndsAtUtc);
        Assert.Equal(1, run.HorseRunNumber);
    }
}
