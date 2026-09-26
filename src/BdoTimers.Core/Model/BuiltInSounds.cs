namespace BdoTimers.Core.Model;

/// <summary>
/// Keys of the alert sounds bundled with the app (Assets/Sounds/&lt;key&gt;.wav; sources in SOURCES.txt). Each is under
/// two seconds: an alert only has to say "look", and anything longer keeps going after it has been heard.
/// </summary>
public static class BuiltInSounds
{
    public const string Marimba = "marimba";
    public const string Ringtone = "ringtone";
    public const string Vibraphone = "vibraphone";
    public const string Tropical = "tropical";
    public const string Melody = "melody";
    public const string Chimes = "chimes";
    public const string Ping = "ping";
    public const string Beeps = "beeps";
    /// <summary>A marimba phrase, like the alarms of today's phone clock apps.</summary>
    public const string Default = Marimba;

    public static IReadOnlyList<string> All { get; } = [Marimba, Ringtone, Vibraphone, Tropical, Melody, Chimes, Ping, Beeps];
}
