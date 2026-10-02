using System.Globalization;
using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Seed;

public static class SeedService
{
    public static BossSeed LoadEmbedded(string regionId = BossRegions.Europe)
    {
        var resource = BossRegions.Find(regionId).ResourceName;
        using var stream = typeof(SeedService).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is missing.");
        return JsonSerializer.Deserialize<BossSeed>(stream, JsonDefaults.Options)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is empty.");
    }

    public static IReadOnlyList<TimerDef> ToTimers(BossSeed seed, AlertConfig alerts, string regionId = BossRegions.Europe) =>
        seed.Bosses.Select(b => new TimerDef
        {
            Name = b.Name,
            Kind = TimerKind.Scheduled,
            IsBuiltIn = true,
            BossRegionId = regionId,
            Alerts = alerts,
            Scheduled = new ScheduledSpec
            {
                TimeZoneId = seed.TimeZoneId,
                Slots = b.Slots
                    .Select(s => new Slot(s.Day, TimeOnly.ParseExact(s.Time, "HH:mm", CultureInfo.InvariantCulture)))
                    .ToList(),
            },
        }).ToList();

    /// <summary>The seed's spawn times for the named boss, or null when the seed has no such boss.</summary>
    public static ScheduledSpec? Schedule(BossSeed seed, string name) => ToTimers(seed, new AlertConfig())
        .FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))?.Scheduled;

    /// <summary>A new install starts with the presets and the accepted EU timetable.</summary>
    public static AppData NewData(BossSeed seed)
    {
        var presets = Presets.Create();
        return BossRegions.WithState(new AppData
        {
            DataVersion = DataMigrations.Current,
            Timers = [.. presets.Take(2), Presets.CreateHorseRegistration(), .. presets.Skip(2), .. ToTimers(seed, new AlertConfig())],
        }, new BossRegionState { SeedApplied = true, AcceptedBossTimetable = seed });
    }

    /// <summary>Copies the seed into the user's timers once; afterwards the user's copy is authoritative.</summary>
    public static AppData ApplyIfNeeded(AppData data, BossSeed seed, AlertConfig alerts, string? regionId = null)
    {
        var id = regionId ?? data.SelectedBossRegion;
        var state = BossRegions.State(data, id);
        if (state.SeedApplied) return TimetableUpdates.InitializeBaseline(data, seed, id);
        // Even a legacy file with a missing seed flag must not duplicate its existing bosses.
        if (data.Timers.Any(t => t.IsBuiltIn && BossRegions.RegionOf(t) == id))
            return TimetableUpdates.InitializeBaseline(BossRegions.WithState(data, state with { SeedApplied = true }), seed, id);
        return BossRegions.WithState(data with { Timers = [.. data.Timers, .. ToTimers(seed, alerts, id)] },
            state with { SeedApplied = true, AcceptedBossTimetable = seed });
    }

    /// <summary>
    /// Replaces the chosen region's timers with the seed's spawn times. A boss still in the seed keeps, by name, its id (which
    /// fired alerts and skipped spawns refer to), its alerts on/off and its alert settings; skipped spawns of bosses no
    /// longer in the seed are dropped.
    /// </summary>
    public static AppData ResetBuiltIns(AppData data, BossSeed seed, AlertConfig alerts, string? regionId = null)
    {
        var id = regionId ?? data.SelectedBossRegion;
        var previous = data.Timers
            .Where(t => t.IsBuiltIn && BossRegions.RegionOf(t) == id)
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var fresh = ToTimers(seed, alerts, id)
            .Select(t => previous.TryGetValue(t.Name, out var old) ? t with { Id = old.Id, Enabled = old.Enabled, Alerts = old.Alerts } : t);
        List<TimerDef> timers = [.. data.Timers.Where(t => !t.IsBuiltIn || BossRegions.RegionOf(t) != id), .. fresh];
        var ids = timers.Select(t => t.Id).ToHashSet();
        return BossRegions.WithState(data with { Timers = timers, Muted = data.Muted.Where(m => ids.Contains(m.TimerId)).ToList() },
            BossRegions.State(data, id) with { SeedApplied = true, AcceptedBossTimetable = seed });
    }
}
