using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Tests;

public class TimetableUpdatesTests
{
    static readonly BossSeed Old = new("Europe/Berlin",
    [new("Kzarka", [new(DayOfWeek.Monday, "19:00")]), new("Nouver", [new(DayOfWeek.Tuesday, "14:00")])], "old source", "2026-09-23");
    static readonly BossSeed New = new("Europe/Berlin",
    [new("Kzarka", [new(DayOfWeek.Monday, "20:00")]), new("Vell", [new(DayOfWeek.Sunday, "16:00")])], "new source", "2026-10-02");

    static AppData Initial => SeedService.ApplyIfNeeded(new AppData(), Old, new AlertConfig());

    [Fact]
    public void Review_reports_changed_added_and_removed_bosses()
    {
        var review = TimetableUpdates.Review(Initial, New);
        Assert.True(review.NeedsReview);
        Assert.Equal(new[] { "Kzarka", "Nouver", "Vell" }, review.Changes.Select(c => c.Name));
        Assert.All(review.Changes, c => Assert.False(c.HasCustomTimes));
        Assert.Null(review.Changes.Single(c => c.Name == "Nouver").Replacement);
        Assert.Null(review.Changes.Single(c => c.Name == "Vell").Current);
    }

    [Fact]
    public void Applying_selected_changes_keeps_ids_alerts_enabled_custom_timers_and_valid_mutes()
    {
        var data = Initial;
        var kzarka = data.Timers.Single(t => t.Name == "Kzarka");
        var nouver = data.Timers.Single(t => t.Name == "Nouver");
        var custom = new TimerDef { Name = "My countdown", Kind = TimerKind.Countdown, Countdown = new() };
        var tuned = new AlertConfig { LeadTimesMinutes = [30], Tts = new TtsAlert { Template = "Custom voice" } };
        var at = new DateTimeOffset(2026, 10, 5, 17, 0, 0, TimeSpan.Zero);
        data = data with
        {
            Timers = [.. data.Timers.Select(t => t.Id == kzarka.Id ? t with { Alerts = tuned, Enabled = false } : t), custom],
            Muted = [new(kzarka.Id, at), new(nouver.Id, at)],
        };
        var changed = TimetableUpdates.Apply(data, New, ["Kzarka", "Nouver", "Vell"]);
        var kept = changed.Timers.Single(t => t.Name == "Kzarka");
        Assert.Equal(kzarka.Id, kept.Id);
        Assert.Same(tuned, kept.Alerts);
        Assert.False(kept.Enabled);
        Assert.Equal(new TimeOnly(20, 0), kept.Scheduled!.Slots[0].Time);
        Assert.Contains(changed.Timers, t => ReferenceEquals(t, custom));
        Assert.DoesNotContain(changed.Timers, t => t.Id == nouver.Id);
        Assert.Equal([new MutedOccurrence(kzarka.Id, at)], changed.Muted);
        Assert.False(TimetableUpdates.Review(changed, New).NeedsReview);
    }

    [Fact]
    public void Custom_times_are_marked_and_can_be_kept_while_other_changes_apply()
    {
        var data = Initial with
        {
            Timers = Initial.Timers.Select(t => t.Name == "Kzarka" ? t with
            { Scheduled = new ScheduledSpec { TimeZoneId = "UTC", Slots = [new(DayOfWeek.Friday, new TimeOnly(12, 0))] } } : t).ToList(),
        };
        var review = TimetableUpdates.Review(data, New);
        Assert.True(review.Changes.Single(c => c.Name == "Kzarka").HasCustomTimes);
        var kept = TimetableUpdates.Apply(data, New, review.Changes.Where(c => !c.HasCustomTimes).Select(c => c.Name));
        Assert.Same(data.Timers.Single(t => t.Name == "Kzarka").Scheduled, kept.Timers.Single(t => t.Name == "Kzarka").Scheduled);
        Assert.False(TimetableUpdates.Review(kept, New).NeedsReview);

        var later = New with { Bosses = [new("Kzarka", [new(DayOfWeek.Monday, "21:00")]), New.Bosses[1]] };
        Assert.True(TimetableUpdates.Review(kept, later).Changes.Single(c => c.Name == "Kzarka").HasCustomTimes);
    }

    [Fact]
    public void Keeping_all_current_times_accepts_the_version_without_changing_timers()
    {
        var data = Initial;
        var kept = TimetableUpdates.Apply(data, New, []);
        Assert.Equal(data.Timers, kept.Timers);
        Assert.Equal(New, kept.AcceptedBossTimetable);
        Assert.False(TimetableUpdates.Review(kept, New).NeedsReview);
    }

    [Fact]
    public void Legacy_installations_get_a_baseline_only_when_all_times_match()
    {
        var legacy = Initial with { AcceptedBossTimetable = null };
        var matched = TimetableUpdates.InitializeBaseline(legacy, Old);
        Assert.Equal(Old, matched.AcceptedBossTimetable);
        var uncertain = TimetableUpdates.InitializeBaseline(legacy, New);
        Assert.Null(uncertain.AcceptedBossTimetable);
        var review = TimetableUpdates.Review(uncertain, New);
        Assert.All(review.Changes.Where(c => c.Current is not null), c => Assert.True(c.HasCustomTimes));
    }

    [Fact]
    public void Metadata_order_and_duplicate_slots_do_not_create_an_update()
    {
        var same = Old with { Source = "updated source", VerifiedOn = "2026-10-02",
            Bosses = [Old.Bosses[1], Old.Bosses[0] with { Slots = [Old.Bosses[0].Slots[0], Old.Bosses[0].Slots[0]] }] };
        Assert.Equal(TimetableUpdates.Revision(Old), TimetableUpdates.Revision(same));
        Assert.False(TimetableUpdates.Review(Initial, same).NeedsReview);
    }

    [Fact]
    public void A_personal_edit_to_an_unchanged_boss_is_preserved_without_becoming_an_update()
    {
        var data = Initial;
        data = data with { Timers = data.Timers.Select(t => t.Name == "Nouver" ? t with { Scheduled = new() } : t).ToList() };
        var next = Old with { Bosses = [New.Bosses[0], Old.Bosses[1]] };
        Assert.Equal("Kzarka", Assert.Single(TimetableUpdates.Review(data, next).Changes).Name);
    }

    [Fact]
    public void Accepted_baseline_survives_json_round_trip()
    {
        var data = Initial;
        var loaded = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data, JsonDefaults.Options), JsonDefaults.Options)!;
        Assert.Equal(TimetableUpdates.Revision(Old), TimetableUpdates.Revision(loaded.AcceptedBossTimetable!));
        Assert.False(TimetableUpdates.Review(loaded, Old).NeedsReview);
    }
}
