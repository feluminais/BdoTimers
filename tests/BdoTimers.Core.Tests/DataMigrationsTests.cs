using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class DataMigrationsTests
{
    [Fact]
    public void Current_data_is_left_alone()
    {
        var data = new AppData
        {
            DataVersion = DataMigrations.Current,
            Timers = [new TimerDef { Name = "Mine", Alerts = new AlertConfig { LeadTimesMinutes = [15, 5, 1, 0] } }],
        };

        Assert.Same(data, DataMigrations.Apply(data, new()));
    }

    [Fact]
    public void Running_horse_preset_becomes_a_separate_run()
    {
        var template = Presets.CreateHorseRegistration();
        template = template with { Countdown = CountdownOps.Start(template.Countdown!, T0) };
        var data = new AppData { DataVersion = 4, Timers = [template] };

        var migrated = DataMigrations.Apply(data, new());

        Assert.Equal(CountdownStatus.Idle, migrated.Timers[0].Countdown!.Status);
        var run = migrated.Timers[1];
        Assert.Equal(Presets.HorseRegistrationRun, run.Preset);
        Assert.Equal(T0.AddMinutes(10), run.Countdown!.EndsAtUtc);
        Assert.Equal(1, run.HorseRunNumber);
        Assert.Equal(DataMigrations.Current, migrated.DataVersion);
    }

    [Fact]
    public void Guild_war_without_times_is_dropped_and_one_with_times_is_kept()
    {
        TimerDef War(params Slot[] slots) => new()
        {
            Name = "Guild war", Kind = TimerKind.Scheduled, Preset = Presets.GuildWar,
            Scheduled = new ScheduledSpec { Slots = slots },
        };
        var own = new TimerDef { Name = "Buff", Kind = TimerKind.Countdown, Countdown = new CountdownSpec() };
        var set = War(new Slot(DayOfWeek.Tuesday, new TimeOnly(20, 0)));

        Assert.Equal([own], DataMigrations.Apply(new AppData { DataVersion = 7, Timers = [War(), own] }, new()).Timers);
        Assert.Equal([set, own], DataMigrations.Apply(new AppData { DataVersion = 7, Timers = [set, own] }, new()).Timers);
    }
}
