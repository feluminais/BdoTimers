namespace BdoTimers.Core.Model;

/// <summary>Keys of the alert sounds bundled with the app (Assets/Sounds/&lt;key&gt;.wav).</summary>
public static class BuiltInSounds
{
    public const string Gong = "gong";
    public const string Horn = "horn";
    public const string Bell = "bell";
    public const string Chime = "chime";
    public const string Default = Gong;

    public static IReadOnlyList<string> All { get; } = [Gong, Horn, Bell, Chime];

    /// <summary>The key itself when bundled, otherwise <see cref="Default"/> (e.g. a key from a newer version).</summary>
    public static string Resolve(string? key) => key is not null && All.Contains(key) ? key : Default;
}
