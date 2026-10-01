using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Sounds;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;

namespace BdoTimers.App.Alerts;

/// <summary>
/// Fans an alert out to its enabled channels. Each channel fails independently; audio
/// (sound then speech) is serialized so simultaneous alerts don't talk over each other. The speech is generated
/// while the sound plays, so it follows without a gap.
/// </summary>
public sealed class AlertDispatcher(
    ToastChannel toast, SoundChannel sound, TtsChannel tts, PersistentState<AppSettings> settings, UserSounds userSounds)
    : IAlertSink
{
    readonly SemaphoreSlim _audio = new(1, 1);

    public void Dispatch(AlertEvent alert) => _ = RunAsync(alert);

    /// <summary>Speaks a short confirmation through the same audio queue as timer alerts.</summary>
    public void Say(string text) => _ = SayAsync(text);

    async Task SayAsync(string text)
    {
        await _audio.WaitAsync();
        try
        {
            var s = settings.Current;
            await TryAsync("tts", async () => await sound.PlayAsync(
                await tts.SynthesizeAsync(text, s.TtsVoice, s.TtsRate), s.Volume));
        }
        finally { _audio.Release(); }
    }

    public void PrepareSpeech() => Try("tts", () => tts.Warm(settings.Current.TtsVoice));

    public void NotifyEndedWhileAway(TimerDef timer) =>
        Try("toast", () => toast.ShowInfo(timer.Name, timer.Countdown?.EndsAtUtc is { } end
            ? $"Countdown ended at {end.ToLocalTime():HH:mm}."
            : "Countdown ended."));

    async Task RunAsync(AlertEvent alert)
    {
        var message = AlertMessage.Build(alert);
        // A shared spawn uses a channel if any of its timers wants it; the sound is the first enabled one's.
        var configs = alert.Timers.Select(t => t.Alerts).ToList();
        if (configs.Any(c => c.Toast.Enabled)) Try("toast", () => toast.ShowUrgent(message));

        await _audio.WaitAsync();
        try
        {
            var s = settings.Current;
            var speech = configs.Any(c => c.Tts.Enabled)
                ? tts.SynthesizeAsync(message.Speech, s.TtsVoice, s.TtsRate)
                : null;
            if (configs.FirstOrDefault(c => c.Sound.Enabled) is { } withSound)
                await TryAsync("sound", () => sound.PlayAsync(
                    SoundKeys.Playable(withSound.Sound.Key, s.AlertSound, userSounds.Exists), s.Volume));
            if (speech is not null)
                await TryAsync("tts", async () => await sound.PlayAsync(await speech, s.Volume));
        }
        finally
        {
            _audio.Release();
        }
    }

    static void Try(string channel, Action action)
    {
        try { action(); }
        catch (Exception ex) { Log.Error($"Alert channel '{channel}' failed", ex); }
    }

    static async Task TryAsync(string channel, Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Log.Error($"Alert channel '{channel}' failed", ex); }
    }
}
