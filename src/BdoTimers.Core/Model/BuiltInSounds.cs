namespace BdoTimers.Core.Model;

/// <summary>Keys of the alert sounds bundled with the app (Assets/Sounds/&lt;key&gt;.wav; sources in SOURCES.txt).</summary>
public static class BuiltInSounds
{
    public const string Horn = "horn";
    public const string Harp = "harp";
    public const string Bowl = "bowl";
    public const string Bell = "bell";
    /// <summary>A low horn: ringing metal (bowl, bell) grates when it repeats all day, so it is never the default.</summary>
    public const string Default = Horn;

    public static IReadOnlyList<string> All { get; } = [Horn, Harp, Bowl, Bell];
}
