using System.Globalization;
using System.Text.Json;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;

namespace BdoTimers.Core.Seed;

public static class SeedService
{
    public const string ResourceName = "BdoTimers.Core.Data.bosses.eu.json";

    public static BossSeed LoadEmbedded()
    {
        using var stream = typeof(SeedService).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        return JsonSerializer.Deserialize<BossSeed>(stream, JsonDefaults.Options)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is empty.");
    }

    public static IReadOnlyList<TimerDef> ToTimers(BossSeed seed, AlertConfig alerts) =>
        seed.Bosses.Select(b => new TimerDef
        {
            Name = b.Name,
            Kind = TimerKind.Scheduled,
            IsBuiltIn = true,
            Alerts = alerts,
            Scheduled = new ScheduledSpec
            {
                TimeZoneId = seed.TimeZoneId,
                Slots = b.Slots
                    .Select(s => new Slot(s.Day, TimeOnly.ParseExact(s.Time, "HH:mm", CultureInfo.InvariantCulture)))
                    .ToList(),
            },
        }).ToList();

    /// <summary>Copies the seed into the user's timers once; afterwards the user's copy is authoritative.</summary>
    public static AppData ApplyIfNeeded(AppData data, BossSeed seed, AlertConfig alerts) =>
        data.SeedApplied
            ? data
            : data with { Timers = [.. data.Timers, .. ToTimers(seed, alerts)], SeedApplied = true };

    /// <summary>Replaces all built-in timers with the seed, keeping each boss's alert settings by name.</summary>
    public static AppData ResetBuiltIns(AppData data, BossSeed seed, AlertConfig alerts)
    {
        var previousAlerts = data.Timers
            .Where(t => t.IsBuiltIn)
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Alerts, StringComparer.OrdinalIgnoreCase);
        var fresh = ToTimers(seed, alerts)
            .Select(t => previousAlerts.TryGetValue(t.Name, out var a) ? t with { Alerts = a } : t);
        return data with { Timers = [.. data.Timers.Where(t => !t.IsBuiltIn), .. fresh], SeedApplied = true };
    }
}
