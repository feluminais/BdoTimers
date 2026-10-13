using System.Windows;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Sounds;
using BdoTimers.Core.Storage;

namespace BdoTimers.App;

/// <summary>
/// Plays a sound or speaks a line once for the ▶ buttons, like an alert would. A new preview stops the previous one,
/// so repeated presses don't pile up. UI thread only.
/// </summary>
public sealed class AudioPreview(
    SoundChannel sound, TtsChannel tts, UserSounds userSounds, PersistentState<AppSettings> settings, AppHealth health)
{
    CancellationTokenSource? _preview;

    /// <summary>Plays a sound at the current volume.</summary>
    public void PlaySound(string? key) => PlaySound(key, settings.Current.Volume);

    public void PlaySound(string? key, float volume) => Run("Sound", cancel => sound.PlayAsync(Playable(key), volume, cancel));

    /// <summary>Speaks <paramref name="text"/> with the chosen voice and speed.</summary>
    public void Speak(string text) => Speak(text, settings.Current);

    public void Speak(string text, AppSettings s) => Run("Voice", async cancel =>
    {
        var speech = await tts.SynthesizeAsync(text, s.TtsVoice, s.TtsRate);
        if (cancel.IsCancellationRequested) speech.Dispose();
        else await sound.PlayAsync(speech, s.Volume, cancel);
    });

    /// <summary>The key that plays for a timer sound key; null means the app-wide alert sound.</summary>
    string Playable(string? key) => SoundKeys.Playable(key, settings.Current.AlertSound, userSounds.Exists);

    async void Run(string area, Func<CancellationToken, Task> play)
    {
        _preview?.Cancel();
        var preview = _preview = new CancellationTokenSource();
        try { await play(preview.Token); if (!preview.IsCancellationRequested) health.Succeeded(area); }
        catch (OperationCanceledException) when (preview.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!preview.IsCancellationRequested)
            {
                Log.Error("Preview failed", ex);
                health.Failed(area, ex);
                MessageBox.Show(health.LastFailure?.Message, "BDO Timers", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (_preview == preview) _preview = null;
            preview.Dispose();
        }
    }
}
