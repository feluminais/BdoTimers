using System.IO;
using BdoTimers.Core.Model;
using BdoTimers.Core.Sounds;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Option lists for sound selectors: the built-in sounds, then the user's sounds by file name.</summary>
public static class SoundChoices
{
    static readonly IReadOnlyDictionary<string, string> BuiltInLabels = new Dictionary<string, string>
    {
        [BuiltInSounds.Marimba] = "Marimba",
        [BuiltInSounds.Ringtone] = "Ringtone",
        [BuiltInSounds.Vibraphone] = "Vibraphone",
        [BuiltInSounds.Tropical] = "Tropical",
        [BuiltInSounds.Melody] = "Melody",
        [BuiltInSounds.Chimes] = "Chimes",
        [BuiltInSounds.Ping] = "Ping",
        [BuiltInSounds.Beeps] = "Digital beeps",
    };

    public static string Label(string key) =>
        BuiltInLabels.TryGetValue(key, out var label) ? label : Path.GetFileNameWithoutExtension(key);

    /// <summary>Choices for the app-wide alert sound; values are sound keys.</summary>
    public static IReadOnlyList<Choice> ForApp(UserSounds sounds) =>
        BuiltInSounds.All.Concat(sounds.Keys()).Select(k => new Choice(Label(k), k)).ToList();

    /// <summary>Choices for a timer: Default, every sound, Off. Values are <see cref="SoundAlert"/> records.</summary>
    public static IReadOnlyList<Choice> ForTimer(UserSounds sounds) =>
    [
        new("Default", new SoundAlert()),
        .. BuiltInSounds.All.Concat(sounds.Keys()).Select(k => new Choice(Label(k), new SoundAlert { Key = k })),
        new("Off", new SoundAlert { Enabled = false }),
    ];

    /// <summary>The choice showing a timer's saved sound; a sound that no longer exists shows as Default.</summary>
    public static Choice Matching(IReadOnlyList<Choice> timerChoices, SoundAlert saved) =>
        !saved.Enabled ? timerChoices[^1]
        : timerChoices.FirstOrDefault(c => c.Value is SoundAlert { Enabled: true } s && s.Key == saved.Key) ?? timerChoices[0];
}
