using System.IO;
using BdoTimers.Core.Text;
using NAudio.Wave;

namespace BdoTimers.App.Alerts;

/// <summary>A voice the user can pick in Settings.</summary>
public sealed record VoiceOption(string Id, string Label);

/// <summary>
/// Turns alert text into speech audio with a Kokoro voice. Names are respelled first (see <see cref="Pronunciation"/>).
/// The audio is returned rather than played, so it can be generated while the alert sound plays and then go through the
/// same playback and volume as the sounds.
/// </summary>
public sealed class TtsChannel(KokoroEngine kokoro) : IDisposable
{
    public IReadOnlyList<VoiceOption> Voices() => KokoroEngine.Voices.Select(v => new VoiceOption(v.Id, v.Label)).ToList();

    /// <summary>Speaks when no voice was chosen, or the chosen one is gone.</summary>
    public string DefaultVoiceId => KokoroEngine.Default.Id;

    /// <summary>Speech audio for <paramref name="text"/>; <paramref name="rate"/> is the Settings speed, -5 to 5.</summary>
    public Task<WaveStream> SynthesizeAsync(string text, string? voiceId, int rate) =>
        Task.Run<WaveStream>(() =>
        {
            if (!kokoro.IsAvailable) throw new FileNotFoundException("The voice model is missing; scripts/get-voice.ps1 fetches it.");
            var voice = KokoroEngine.Find(voiceId) ?? KokoroEngine.Default;
            // Settings' -5..5 maps to 0.6x..1.4x.
            var samples = kokoro.Generate(Pronunciation.Apply(text), voice, 1f + Math.Clamp(rate, -5, 5) * 0.08f, out var sampleRate);
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        });

    /// <summary>Loads a voice's model in the background, so speech that follows soon starts sooner.</summary>
    public void Warm(string? voiceId) => kokoro.Warm(KokoroEngine.Find(voiceId) ?? KokoroEngine.Default);

    public void Dispose() => kokoro.Dispose();
}
