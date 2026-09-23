using System.Speech.Synthesis;

namespace BdoTimers.App.Alerts;

public sealed class TtsChannel : IDisposable
{
    readonly SpeechSynthesizer _synth = new();

    public TtsChannel() => _synth.SetOutputToDefaultAudioDevice();

    public IReadOnlyList<string> InstalledVoices() =>
        _synth.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name).ToList();

    public Task SpeakAsync(string text, string? voice, int rate, float volume)
    {
        if (!string.IsNullOrEmpty(voice) && InstalledVoices().Contains(voice)) _synth.SelectVoice(voice);
        _synth.Rate = Math.Clamp(rate, -10, 10);
        _synth.Volume = (int)Math.Round(Math.Clamp(volume, 0f, 1f) * 100);

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SpeakCompletedEventArgs>? handler = null;
        handler = (_, e) =>
        {
            _synth.SpeakCompleted -= handler;
            if (e.Error is not null) done.TrySetException(e.Error);
            else done.TrySetResult();
        };
        _synth.SpeakCompleted += handler;
        _synth.SpeakAsync(text);
        return done.Task;
    }

    public void Dispose() => _synth.Dispose();
}
