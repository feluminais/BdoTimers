using System.Globalization;
using System.Diagnostics;
using System.IO;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Text;
using NAudio.Wave;

namespace BdoTimers.App.Alerts;

/// <summary>
/// Turns alert text into speech audio with a Kokoro voice. Names are respelled first (see <see cref="Pronunciation"/>).
/// The audio is returned rather than played, so it can be generated while the alert sound plays and then go through the
/// same playback and volume as the sounds. Generated speech is kept in <paramref name="cache"/>, as alerts repeat the
/// same lines every week, and upcoming alerts' lines are prepared ahead (see <see cref="Prepare"/>).
/// </summary>
public sealed class TtsChannel(KokoroEngine kokoro, SpeechCache cache) : IDisposable
{
    // One line is synthesized at a time, so speech wanted while its line is being prepared waits for it instead of
    // synthesizing it twice.
    readonly object _synthesis = new();
    readonly object _gate = new();
    readonly Queue<(string Key, Line Line)> _queue = new();
    // Lines asked for ahead this session, so repeats are cheap and a line that fails isn't retried every tick.
    readonly HashSet<(string Text, string? VoiceId, int Rate)> _asked = [];
    bool _preparing;
    volatile bool _disposed;

    /// <summary>
    /// Speech audio for <paramref name="text"/>, from the cache or else synthesized and cached; <paramref name="rate"/>
    /// is the Settings speed, -5 to 5.
    /// </summary>
    public Task<WaveStream> SynthesizeAsync(string text, string? voiceId, int rate) =>
        Task.Run<WaveStream>(() =>
        {
            var duration = Stopwatch.StartNew();
            if (!kokoro.IsAvailable) throw new FileNotFoundException("Voice files are missing or incomplete. Repair the installation.");
            var line = Line.For(text, voiceId, rate);
            var key = line.Key(kokoro.ModelIdentity);
            if (cache.Open(key) is { } cached)
            {
                PerformanceMetrics.SpeechReady(duration.ElapsedMilliseconds, cached: true);
                return cached;
            }
            lock (_synthesis)
            {
                if (cache.Open(key) is { } prepared)
                {
                    PerformanceMetrics.SpeechReady(duration.ElapsedMilliseconds, cached: true);
                    return prepared;
                }
                var samples = kokoro.Generate(line.Spoken, line.Voice, line.Speed, out var sampleRate);
                Store(key, samples, sampleRate);
                var bytes = new byte[samples.Length * sizeof(float)];
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
                PerformanceMetrics.SpeechReady(duration.ElapsedMilliseconds, cached: false);
                return new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
            }
        });

    /// <summary>
    /// Synthesizes lines into the cache before the alerts that speak them. Returns at once: one below-normal thread
    /// works through the lines not cached yet, then unloads the model unless more are queued or on-demand speech is
    /// keeping it. A line already asked for costs a set lookup, as the scheduler asks every tick until it's spoken.
    /// </summary>
    public void Prepare(IReadOnlyCollection<string> texts, string? voiceId, int rate)
    {
        lock (_gate)
        {
            var fresh = texts.Where(text => _asked.Add((text, voiceId, rate))).ToList();
            if (fresh.Count == 0 || _disposed || !kokoro.IsAvailable) return;
            var model = kokoro.ModelIdentity;
            foreach (var line in fresh.Select(text => Line.For(text, voiceId, rate))) _queue.Enqueue((line.Key(model), line));
            if (_preparing) return;
            _preparing = true;
        }
        // Its own thread, so the low priority doesn't stick to a pool thread; one inference thread runs on it.
        new Thread(PrepareQueued) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Speech preparation" }
            .Start();
    }

    void PrepareQueued()
    {
        while (true)
        {
            (string Key, Line Line) next;
            lock (_gate)
            {
                if (_disposed || !_queue.TryDequeue(out next))
                {
                    _preparing = false;
                    return;
                }
            }
            try
            {
                lock (_synthesis)
                {
                    if (!cache.Contains(next.Key))
                    {
                        var samples = kokoro.Generate(next.Line.Spoken, next.Line.Voice, next.Line.Speed, out var sampleRate, ahead: true);
                        Store(next.Key, samples, sampleRate);
                    }
                }
                bool more;
                lock (_gate) more = _queue.Count > 0;
                if (!more) kokoro.Release();
            }
            // Nothing may escape a thread of its own; the line is synthesized when its alert plays instead.
            catch (Exception ex)
            {
                if (!_disposed) Log.Error($"Couldn't prepare speech \"{next.Line.Spoken}\"", ex);
            }
        }
    }

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

    /// <summary>
    /// Kokoro's speed for the Settings speed, -5 to 5. Kokoro's own pace (1x) is brisk conversation, about five
    /// syllables a second, so the middle is 0.85x, about four; each step is 8% faster or slower, 0.58x to 1.25x.
    /// </summary>
    static float Speed(int rate) => 0.85f * MathF.Pow(1.08f, Math.Clamp(rate, -5, 5));

    /// <summary>Loads a voice's model in the background, so speech that follows soon starts sooner.</summary>
    public void Warm(string? voiceId) => kokoro.Warm(KokoroEngine.Find(voiceId) ?? KokoroEngine.Default);

    public void Dispose()
    {
        _disposed = true;
        lock (_gate) _queue.Clear();
        kokoro.Dispose();
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
}
