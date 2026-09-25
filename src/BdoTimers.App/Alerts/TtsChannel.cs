using System.IO;
using System.Speech.Synthesis;
using BdoTimers.Core.Text;
using NAudio.Wave;

namespace BdoTimers.App.Alerts;

/// <summary>A voice the user can pick in Settings.</summary>
public sealed record VoiceOption(string Id, string Label);

/// <summary>
/// Turns alert text into speech audio: a Kokoro voice when the model is installed, otherwise a Windows voice. Names are
/// respelled first (see <see cref="Pronunciation"/>). The audio is returned rather than played, so it can be generated
/// while the alert sound plays and then go through the same playback and volume as the sounds.
/// </summary>
public sealed class TtsChannel(KokoroEngine kokoro) : IDisposable
{
    readonly SpeechSynthesizer _windows = new();

    public IReadOnlyList<VoiceOption> Voices() =>
    [
        .. kokoro.IsAvailable ? KokoroEngine.Voices.Select(v => new VoiceOption(v.Id, v.Label)) : [],
        .. WindowsVoices().Select(name => new VoiceOption(name, name)),
    ];

    /// <summary>Speaks when no voice was chosen, or the chosen one is gone.</summary>
    public string DefaultVoiceId => kokoro.IsAvailable ? KokoroEngine.Default.Id : WindowsVoices().FirstOrDefault() ?? "";

    /// <summary>Speech audio for <paramref name="text"/>; <paramref name="rate"/> is the Settings speed, -5 to 5.</summary>
    public Task<WaveStream> SynthesizeAsync(string text, string? voiceId, int rate) =>
        Task.Run(() =>
        {
            var spoken = Pronunciation.Apply(text);
            var id = Voices().Any(v => v.Id == voiceId) ? voiceId! : DefaultVoiceId;
            return KokoroEngine.Find(id) is { } voice ? Kokoro(spoken, voice, rate) : Windows(spoken, id, rate);
        });

    WaveStream Kokoro(string text, KokoroVoice voice, int rate)
    {
        // Settings' -5..5 maps to 0.6x..1.4x.
        var samples = kokoro.Generate(text, voice, 1f + Math.Clamp(rate, -5, 5) * 0.08f, out var sampleRate);
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
    }

    WaveStream Windows(string text, string voiceName, int rate)
    {
        lock (_windows)
        {
            if (WindowsVoices().Contains(voiceName)) _windows.SelectVoice(voiceName);
            _windows.Rate = Math.Clamp(rate, -10, 10);
            var wav = new MemoryStream();
            _windows.SetOutputToWaveStream(wav);
            try { _windows.Speak(text); }
            finally { _windows.SetOutputToNull(); }
            wav.Position = 0;
            return new WaveFileReader(wav);
        }
    }

    IReadOnlyList<string> WindowsVoices()
    {
        lock (_windows) return _windows.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name).ToList();
    }

    public void Dispose()
    {
        _windows.Dispose();
        kokoro.Dispose();
    }
}
