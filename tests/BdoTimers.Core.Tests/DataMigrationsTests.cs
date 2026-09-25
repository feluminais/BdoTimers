using BdoTimers.Core.Model;
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
    public void Current_data_is_left_alone()
    {
        var data = new AppData { DataVersion = DataMigrations.Current, Timers = [With("Mine", [15, 5, 1, 0])] };

        Assert.Same(data, DataMigrations.Apply(data, new AppSettings()));
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
}
