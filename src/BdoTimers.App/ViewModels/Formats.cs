using System.Globalization;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App.ViewModels;

/// <summary>Text and keys the screens and the overlay build alike.</summary>
public static class Formats
{
    /// <summary>A moment as a local weekday and time: "Fri 03:00".</summary>
    public static string DayTime(DateTimeOffset at) => at.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A moment as a local time: "03:00".</summary>
    public static string Time(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The same while a spawn keeps its time and bosses, so what's shown for it is built only when that changes.</summary>
    public static string SpawnKey(SpawnGroup group) => $"{group.AtUtc:O}|{string.Join(",", group.Bosses.Select(b => b.Id))}";
}
