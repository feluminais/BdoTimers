using BdoTimers.Core.Model;

namespace BdoTimers.Core.Sounds;

/// <summary>
/// A sound key is a built-in key (<see cref="BuiltInSounds"/>) or a user sound's file name inside the sounds folder.
/// User keys always have an extension, so they never clash with built-in keys.
/// </summary>
public static class SoundKeys
{
    public static bool IsBuiltIn(string key) => BuiltInSounds.All.Contains(key);

    public static bool IsUserKey(string key) =>
        Path.HasExtension(key) && Path.GetFileName(key) == key && key.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>False for a built-in sound the app no longer has; a user key counts even if its file is gone.</summary>
    public static bool IsKnown(string key) => IsBuiltIn(key) || IsUserKey(key);

    /// <summary>
    /// The sound to play for a timer: its own key if playable, else the app-wide sound if playable, else the built-in
    /// default. Covers keys from a newer version and user sounds that were removed.
    /// </summary>
    public static string Playable(string? key, string appDefault, Func<string, bool> userSoundExists)
    {
        foreach (var candidate in new[] { key, appDefault })
            if (candidate is not null && (IsBuiltIn(candidate) || IsUserKey(candidate) && userSoundExists(candidate)))
                return candidate;
        return BuiltInSounds.Default;
    }
}
