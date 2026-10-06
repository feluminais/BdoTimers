using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimers;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public sealed class SetMutedTests : IDisposable
{
    static readonly DateTimeOffset At = Utc(22, 17, 0);

    readonly TempDir _dir = new();
    readonly TimerDef _kzarka = Boss("Kzarka", DayOfWeek.Tuesday, 19);
    readonly TimerDef _nouver = Boss("Nouver", DayOfWeek.Tuesday, 19);
    readonly TimerStore _timers;

    public SetMutedTests() => _timers = new(new(_dir.File("timers.json"), () => new()), new AppData { Timers = [_kzarka, _nouver] });

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void A_group_is_skipped_and_unskipped_in_one_update()
    {
        var changes = 0;
        _timers.Changed += () => changes++;
        (Guid, DateTimeOffset)[] group = [(_kzarka.Id, At), (_nouver.Id, At)];

        _timers.SetMuted(group, true);

        Assert.Equal(1, changes);
        Assert.Equal([new MutedOccurrence(_kzarka.Id, At), new MutedOccurrence(_nouver.Id, At)],
            _timers.Current.Muted.OrderBy(m => m.TimerId == _kzarka.Id ? 0 : 1));

        _timers.SetMuted(group, false);

        Assert.Equal(2, changes);
        Assert.Empty(_timers.Current.Muted);
    }

    [Fact]
    public void Skipping_what_is_already_skipped_changes_nothing_and_other_skips_stay()
    {
        var other = new MutedOccurrence(_kzarka.Id, At.AddDays(1));
        _timers.SetMuted([(_kzarka.Id, At.AddDays(1))], true);
        _timers.SetMuted([(_kzarka.Id, At)], true);
        _timers.SetMuted([(_kzarka.Id, At)], true);

        Assert.Equal(2, _timers.Current.Muted.Count);
        Assert.Contains(other, _timers.Current.Muted);

        _timers.SetMuted([(_kzarka.Id, At)], false);

        Assert.Equal([other], _timers.Current.Muted);
    }
}
