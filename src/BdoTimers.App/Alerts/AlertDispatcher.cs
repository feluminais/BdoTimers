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
    ToastChannel toast, SoundChannel sound, TtsChannel tts, PersistentState<AppSettings> settings, UserSounds userSounds,
    TimerStore timers, AppHealth? health = null)
    : IAlertSink
{
    readonly SemaphoreSlim _audio = new(1, 1);

    public void Dispatch(AlertEvent alert) => _ = RunAsync(alert);

    /// <summary>An explicit test has no saved timer; normal alerts keep every eligibility recheck.</summary>
    public Task SendTestAsync(AlertEvent alert) => RunAsync(alert, test: true);

    internal static AlertEvent? Eligible(AppData data, AlertEvent alert, bool test) => test ? alert : AlertEligibility.Filter(data, alert);

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

    public void PrepareSpeech(IReadOnlyCollection<string> texts) => Try("tts", () =>
    {
        var s = settings.Current;
        tts.Prepare(texts, s.TtsVoice, s.TtsRate);
    }, clearFailure: false);

    public void NotifyEndedWhileAway(TimerDef timer) =>
        Try("toast", () => toast.ShowInfo(timer.Name, timer.OneTime is { } oneTime
            ? $"Missed event at {OneTimeEvents.AtUtc(oneTime).ToLocalTime():MMM d, HH:mm}."
            : timer.Countdown?.EndsAtUtc is { } end
            ? $"Countdown ended at {end.ToLocalTime():HH:mm}."
            : "Countdown ended."));

    async Task RunAsync(AlertEvent planned, bool test = false)
    {
        if (Eligible(timers.Current, planned, test) is not { } alert) return;
        var message = AlertMessage.Build(alert);
        // A shared spawn uses a channel if any of its timers wants it; the sound is the first enabled one's.
        var configs = alert.Timers.Select(t => t.Alerts).ToList();
        if (configs.Any(c => c.Toast.Enabled)) Try("toast", () => toast.ShowUrgent(message));

        await _audio.WaitAsync();
        try
        {
            // Sound may have waited behind another alert while the player changed regions.
            if (Eligible(timers.Current, planned, test) is not { } current) return;
            configs = current.Timers.Select(t => t.Alerts).ToList();
            message = AlertMessage.Build(current);
            var s = settings.Current;
            var speech = configs.Any(c => c.Tts.Enabled)
                ? tts.SynthesizeAsync(message.Speech, s.TtsVoice, s.TtsRate)
                : null;
            if (configs.FirstOrDefault(c => c.Sound.Enabled) is { } withSound)
                await TryAsync("sound", () => sound.PlayAsync(
                    SoundKeys.Playable(withSound.Sound.Key, s.AlertSound, userSounds.Exists), s.Volume));
            if (speech is not null)
                await TryAsync("tts", async () =>
                {
                    var spoken = current;
                    var pending = speech;
                    // Recheck after every synthesis, keeping custom timers when a shared boss becomes inactive.
                    while (true)
                    {
                        using var source = await pending;
                        if (Eligible(timers.Current, spoken, test) is not { } eligible
                            || !eligible.Timers.Any(t => t.Alerts.Tts.Enabled)) return false;
                        if (ReferenceEquals(eligible, spoken))
                        {
                            await sound.PlayAsync(source, s.Volume);
                            return true;
                        }
                        spoken = eligible;
                        pending = tts.SynthesizeAsync(AlertMessage.Build(spoken).Speech, s.TtsVoice, s.TtsRate);
                    }
                });
        }
        finally
        {
            _audio.Release();
        }
    }

    void Try(string channel, Action action, bool clearFailure = true)
    {
        try { action(); if (clearFailure) health?.Succeeded(Area(channel)); }
        catch (Exception ex) { Log.Error($"Alert channel '{channel}' failed", ex); health?.Failed(Area(channel), ex); }
    }

    async Task TryAsync(string channel, Func<Task> action)
    {
        try { await action(); health?.Succeeded(Area(channel)); }
        catch (Exception ex) { Log.Error($"Alert channel '{channel}' failed", ex); health?.Failed(Area(channel), ex); }
    }

    async Task TryAsync(string channel, Func<Task<bool>> action)
    {
        try { if (await action()) health?.Succeeded(Area(channel)); }
        catch (Exception ex) { Log.Error($"Alert channel '{channel}' failed", ex); health?.Failed(Area(channel), ex); }
    }

    static string Area(string channel) => channel switch { "tts" => "Voice", "sound" => "Sound", _ => "Notifications" };
}
