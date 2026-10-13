using BdoTimers.App.Alerts;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

/// <summary>The bundled boss timetable per region: choosing a region, taking in or resetting its updates, and telling
/// the player when a new one is out.</summary>
public sealed class BossTimetable(
    IReadOnlyDictionary<string, BossSeed> seeds, TimerStore timers, UndoService undo, ToastChannel toast, IClock clock)
{
    public void SelectRegion(string regionId)
    {
        timers.SelectBossRegion(regionId, clock);
        NotifyReview();
    }

    public void Apply(IEnumerable<string> names) => ReleasingDroppedPictures(() =>
        timers.Update(data => TimetableUpdates.Apply(data, seeds[data.SelectedBossRegion], names)));

    /// <summary>Brings back the selected region's bundled bosses and spawn times; bosses the player added stay.</summary>
    public void Reset() => ReleasingDroppedPictures(() =>
        timers.Update(d => SeedService.ResetBuiltIns(d, seeds[d.SelectedBossRegion], new AlertConfig())));

    /// <summary>The spawn times this version ships for a boss, or null when its region's timetable no longer has it.</summary>
    public ScheduledSpec? BundledSchedule(TimerDef boss) =>
        seeds.TryGetValue(BossRegions.RegionOf(boss), out var seed) ? SeedService.Schedule(seed, boss.Name) : null;

    /// <summary>Tells the player once per revision that the bundled timetable has changes to review.</summary>
    public void NotifyReview()
    {
        var data = timers.Current;
        var seed = seeds[data.SelectedBossRegion];
        var timetableRevision = TimetableUpdates.Revision(seed);
        if (TimetableUpdates.Review(data, seed).NeedsReview
            && BossRegions.State(data).TimetableNoticeRevision != timetableRevision
            && toast.ShowInfo($"Review the {BossRegions.Find(data.SelectedBossRegion).ShortLabel} timetable",
                "Bundled spawn times can be reviewed in Settings → Bosses."))
        {
            try
            {
                timers.Update(d => BossRegions.WithState(d,
                    BossRegions.State(d, data.SelectedBossRegion) with { TimetableNoticeRevision = timetableRevision }));
            }
            catch (Exception ex) { Log.Error("Couldn't save that the timetable notice was shown", ex); }
        }
    }

    /// <summary>Deletes the pictures of bosses that <paramref name="change"/> removed from the timetable.</summary>
    void ReleasingDroppedPictures(Action change)
    {
        var before = timers.Current.Timers.Select(t => t.ImageFile).OfType<string>().ToList();
        change();
        foreach (var image in before) undo.ReleasePicture(image);
    }
}
