using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

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

        Assert.Same(data, DataMigrations.Apply(data));
    }

    [Fact]
    public void Running_horse_preset_becomes_a_separate_run()
    {
        var now = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        var template = Presets.CreateHorseRegistration();
        template = template with { Countdown = CountdownOps.Start(template.Countdown!, now) };
        var data = new AppData { DataVersion = 4, Timers = [template] };

        var migrated = DataMigrations.Apply(data);

        Assert.Equal(CountdownStatus.Idle, migrated.Timers[0].Countdown!.Status);
        var run = migrated.Timers[1];
        Assert.Equal(Presets.HorseRegistrationRun, run.Preset);
        Assert.Equal(now.AddMinutes(10), run.Countdown!.EndsAtUtc);
        Assert.Equal(1, run.HorseRunNumber);
        Assert.Equal(DataMigrations.Current, migrated.DataVersion);
    }
}
