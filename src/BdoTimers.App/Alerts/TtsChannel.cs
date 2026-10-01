using System.Globalization;
using System.IO;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Text;
using NAudio.Wave;

namespace BdoTimers.App.Alerts;

/// <summary>A voice the user can pick in Settings.</summary>
public sealed record VoiceOption(string Id, string Label);

/// <summary>
/// Turns alert text into speech audio with a Kokoro voice. Names are respelled first (see <see cref="Pronunciation"/>).
/// The audio is returned rather than played, so it can be generated while the alert sound plays and then go through the
/// same playback and volume as the sounds. Generated speech is kept in <paramref name="cache"/>, as alerts repeat the
/// same lines every week.
/// </summary>
public sealed class TtsChannel(KokoroEngine kokoro, SpeechCache cache) : IDisposable
{
    public IReadOnlyList<VoiceOption> Voices() => KokoroEngine.Voices.Select(v => new VoiceOption(v.Id, v.Label)).ToList();

    /// <summary>Speaks when no voice was chosen, or the chosen one is gone.</summary>
    public string DefaultVoiceId => KokoroEngine.Default.Id;

    /// <summary>
    /// Speech audio for <paramref name="text"/>, from the cache or else synthesized and cached; <paramref name="rate"/>
    /// is the Settings speed, -5 to 5.
    /// </summary>
    public Task<WaveStream> SynthesizeAsync(string text, string? voiceId, int rate) =>
        Task.Run<WaveStream>(() =>
        {
            if (!kokoro.IsAvailable) throw new FileNotFoundException("The voice model is missing; scripts/get-voice.ps1 fetches it.");
            var line = Line.For(text, voiceId, rate);
            var key = line.Key(kokoro.ModelIdentity);
            if (cache.Open(key) is { } cached) return cached;
            var samples = kokoro.Generate(line.Spoken, line.Voice, line.Speed, out var sampleRate);
            Store(key, samples, sampleRate);
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        });

    /// <summary>Deletes cached speech unused for a month, on a low-priority background thread.</summary>
    public void CleanCacheInBackground() =>
        new Thread(() =>
        {
            try { cache.DeleteUnused(); }
            catch (Exception ex) { Log.Error("Couldn't clean the speech cache", ex); }
        })
        { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Speech cache cleanup" }.Start();

    void Store(string key, float[] samples, int sampleRate)
    {
        try { cache.Store(key, samples, sampleRate); }
        // The speech still plays; it is synthesized again next time.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Error("Couldn't cache speech", ex); }
    }

    /// <summary>What Kokoro is asked to say: the respelled text, the voice and Kokoro's speed.</summary>
    sealed record Line(string Spoken, KokoroVoice Voice, float Speed)
    {
        public static Line For(string text, string? voiceId, int rate) =>
            new(Pronunciation.Apply(text), KokoroEngine.Find(voiceId) ?? KokoroEngine.Default, TtsChannel.Speed(rate));

        public string Key(string modelIdentity) => SpeechCache.Key(
            modelIdentity, Voice.Key, Voice.SpeakerId.ToString(CultureInfo.InvariantCulture), Voice.British ? "en" : "en-us",
            Speed.ToString("R", CultureInfo.InvariantCulture), Spoken);
    }

    /// <summary>
    /// Kokoro's speed for the Settings speed, -5 to 5. Kokoro's own pace (1x) is brisk conversation, about five
    /// syllables a second, so the middle is 0.85x, about four; each step is 8% faster or slower, 0.58x to 1.25x.
    /// </summary>
    static float Speed(int rate) => 0.85f * MathF.Pow(1.08f, Math.Clamp(rate, -5, 5));

    /// <summary>Loads a voice's model in the background, so speech that follows soon starts sooner.</summary>
    public void Warm(string? voiceId) => kokoro.Warm(KokoroEngine.Find(voiceId) ?? KokoroEngine.Default);

    public void Dispose() => kokoro.Dispose();
}
