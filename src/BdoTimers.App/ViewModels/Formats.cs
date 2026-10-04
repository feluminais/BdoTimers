using System.Globalization;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.ViewModels;

/// <summary>Text and keys the screens and the overlay build alike.</summary>
public static class Formats
{
    /// <summary>A moment as a local weekday and time: "Fri 03:00".</summary>
    public static string DayTime(DateTimeOffset at) => at.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A moment as a local time: "03:00".</summary>
    public static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A moment as a clock at its own offset shows it: "03:00".</summary>
    public static string ZoneTime(DateTimeOffset at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A time of day: "03:00".</summary>
    public static string Time(TimeOnly at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Horse registrations against their limit: "3 of 10 active".</summary>
    public static string HorseRegistrations(int active) => $"{active} of {TimerStore.MaxHorseRegistrations} active";

    /// <summary>The same while a spawn keeps its time and bosses and they keep their names and pictures, so what's shown for
    /// it is built only when that changes.</summary>
    public static string SpawnKey(SpawnGroup group) =>
        $"{group.AtUtc:O}|{string.Join(",", group.Bosses.Select(b => $"{b.Id}:{b.Name}:{b.ImageFile}"))}";
}
