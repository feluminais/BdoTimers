using BdoTimers.Core.Model;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Option lists for overlay pop-up selectors: Off, then "N min before". Values are minutes, 0 for Off.</summary>
public static class PopUpChoices
{
    /// <summary>For a timer's own pop-up.</summary>
    public static readonly IReadOnlyList<int> TimerMinutes = [1, 2, 3, 5, 10, 15, 30];

    /// <summary>For the Guild bosses pop-up; a guild boss takes longer to get ready for.</summary>
    public static readonly IReadOnlyList<int> GuildBossMinutes = [5, 10, 15, 30, 60];

    /// <summary>For a listed timer's overlay row, which shows inside the window without bringing the overlay up.</summary>
    public static readonly IReadOnlyList<int> RowMinutes = [60, 120, 180, 360, 720, 1440];

    /// <summary>Off and <see cref="RowMinutes"/> as hours, plus the saved minutes when they aren't among them.</summary>
    public static IReadOnlyList<Choice> ForRow(int savedMinutes) =>
        [new Choice("Off", 0), .. RowMinutes.Append(savedMinutes).Where(m => m > 0).Distinct().Order().Select(m => new Choice(RowLabel(m), m))];

    static string RowLabel(int minutes) => minutes % 60 == 0 ? $"{minutes / 60} h before" : $"{minutes} min before";

    /// <summary>Off and <paramref name="minutes"/>, plus the saved minutes when they aren't among them.</summary>
    public static IReadOnlyList<Choice> For(IEnumerable<int> minutes, OverlayAlert saved) =>
        [new Choice("Off", 0), .. minutes.Union([saved.ShowMinutesBefore]).Order().Select(m => new Choice($"{m} min before", m))];

    public static Choice Matching(IReadOnlyList<Choice> choices, OverlayAlert saved) =>
        saved.Enabled ? choices.FirstOrDefault(c => (int)c.Value! == saved.ShowMinutesBefore) ?? choices[0] : choices[0];

    public static OverlayAlert Apply(OverlayAlert alert, Choice choice) =>
        choice.Value is int minutes and > 0 ? alert with { Enabled = true, ShowMinutesBefore = minutes } : alert with { Enabled = false };
}
