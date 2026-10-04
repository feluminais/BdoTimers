using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using BdoTimers.Core.Diagnostics;
using SherpaOnnx;

namespace BdoTimers.App.Alerts;

/// <summary>A Kokoro voice: its speaker id in voices.bin and whether it speaks British English.</summary>
public sealed record KokoroVoice(string Key, int SpeakerId, string Label, bool British)
{
    public string Id => KokoroEngine.IdPrefix + Key;
}

/// <summary>
/// Offline neural speech (Kokoro v1.0 through sherpa-onnx), from the model in <paramref name="modelDir"/>. The model
/// holds about 300 MB while loaded, so it loads on first use and is released after a few idle minutes, or right after
/// a batch of speech prepared ahead.
/// </summary>
public sealed class KokoroEngine(string modelDir) : IDisposable
{
    public const string IdPrefix = "kokoro:";
    static readonly TimeSpan IdleUnload = TimeSpan.FromMinutes(3);

    /// <summary>The English voices offered in Settings; speaker ids follow the model's voices.bin.</summary>
    public static IReadOnlyList<KokoroVoice> Voices { get; } =
    [
        new("af_heart", 3, "Heart (US)", false),
        new("af_bella", 2, "Bella (US)", false),
        new("am_michael", 16, "Michael (US)", false),
        new("am_fenrir", 14, "Fenrir (US)", false),
        new("bf_emma", 21, "Emma (UK)", true),
        new("bm_george", 26, "George (UK)", true),
        new("bm_fable", 25, "Fable (UK)", true),
    ];

    public static KokoroVoice Default => Voices[0];

    readonly object _lock = new();
    // Read without the lock by Warm, which only needs a hint.
    volatile OfflineTts? _tts;
    volatile bool _ttsBritish;
    // Guards the idle timer, so Warm can keep the model loaded without waiting for speech being generated.
    readonly object _keepLock = new();
    Timer? _unload;
    // True while on-demand speech or a warm-up keeps the model loaded until the idle timer runs out.
    volatile bool _kept;
    volatile bool _disposed;
    int _warming;
    readonly Lazy<string> _modelIdentity = new(() => string.Join(' ', new[] { "model.int8.onnx", "voices.bin" }
        .Select(name =>
        {
            using var file = File.OpenRead(Path.Combine(modelDir, name));
            return Convert.ToHexStringLower(SHA256.HashData(file));
        }).Append(typeof(OfflineTts).Assembly.GetName().Version?.ToString())));

    /// <summary>
    /// True when every file the model needs is there and voices.bin is whole: sherpa-onnx ends the process on a missing
    /// file or a voices.bin that doesn't match the model, instead of throwing.
    /// </summary>
    public bool IsAvailable =>
        File.Exists(ModelFile("model.int8.onnx")) && VoicesAreWhole(new FileInfo(ModelFile("voices.bin")))
        && File.Exists(ModelFile("tokens.txt")) && Directory.Exists(ModelFile("espeak-ng-data"));

    /// <summary>voices.bin holds a 510 x 256 float style table per speaker, and must reach the highest speaker used.</summary>
    static bool VoicesAreWhole(FileInfo voices)
    {
        const long perSpeaker = 510 * 256 * sizeof(float);
        return voices.Exists && voices.Length % perSpeaker == 0 && voices.Length / perSpeaker > Voices.Max(v => v.SpeakerId);
    }

    /// <summary>Changes with the model, its voices or the speech runtime, so speech they made isn't reused after an update.
    /// Call only while <see cref="IsAvailable"/>.</summary>
    public string ModelIdentity => _modelIdentity.Value;

    public static KokoroVoice? Find(string? id) => Voices.FirstOrDefault(v => v.Id == id);

    /// <summary>
    /// Mono samples at <paramref name="sampleRate"/>. Blocks while it loads the model and generates; call off the UI
    /// thread. The model then stays loaded a few idle minutes for speech that follows, except for speech generated
    /// <paramref name="ahead"/> of its alert, which leaves it to <see cref="Release"/>.
    /// </summary>
    public float[] Generate(string text, KokoroVoice voice, float speed, out int sampleRate, bool ahead = false)
    {
        lock (_lock)
        {
            var tts = Engine(voice.British);
            var audio = tts.Generate(text, speed, voice.SpeakerId);
            try
            {
                sampleRate = audio.SampleRate;
                return audio.Samples;
            }
            finally
            {
                audio.Dispose();
                if (!ahead) Keep();
            }
        }
    }

    /// <summary>Unloads the model after speech prepared ahead, unless on-demand speech or a warm-up is keeping it.</summary>
    public void Release()
    {
        lock (_lock)
        {
            if (!_kept) Free();
        }
    }

    /// <summary>
    /// Loads the model in the background, unless it's already loaded for this voice's English, and keeps it loaded a
    /// while longer. Returns at once; called ahead of speech so the speech doesn't wait for the model.
    /// </summary>
    public void Warm(KokoroVoice voice)
    {
        if (!IsAvailable) return;
        if (_tts is not null && _ttsBritish == voice.British)
        {
            Keep();
            return;
        }
        if (Interlocked.Exchange(ref _warming, 1) == 1) return;
        Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    Engine(voice.British);
                    Keep();
                }
            }
            catch (Exception ex) { Log.Error("Couldn't load the voice model", ex); }
            finally { Volatile.Write(ref _warming, 0); }
        });
    }

    /// <summary>US and UK voices need espeak-ng's American or British English, which is fixed per engine.</summary>
    OfflineTts Engine(bool british)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_tts is not null && _ttsBritish == british) return _tts;
        _tts?.Dispose();
        var loading = Stopwatch.StartNew();
        var config = new OfflineTtsConfig();
        config.Model.Kokoro.Model = ModelFile("model.int8.onnx");
        config.Model.Kokoro.Voices = ModelFile("voices.bin");
        config.Model.Kokoro.Tokens = ModelFile("tokens.txt");
        config.Model.Kokoro.DataDir = ModelFile("espeak-ng-data");
        // espeak-ng's "en" is British English. English goes through espeak-ng in this model, so no lexicon is loaded.
        config.Model.Kokoro.Lang = british ? "en" : "en-us";
        // One thread runs inference on the calling thread, so the caller's priority applies and no worker pool spins.
        // More threads speak a short line no sooner but burn two to four times the CPU, which a game feels.
        config.Model.NumThreads = 1;
        config.Model.Provider = "cpu";
        config.MaxNumSentences = 1;
        _tts = new OfflineTts(config);
        _ttsBritish = british;
        Log.Info($"Kokoro voice loaded ({(british ? "UK" : "US")} English) in {loading.ElapsedMilliseconds} ms");
        return _tts;
    }

    string ModelFile(string name) => Path.Combine(modelDir, name);

    /// <summary>Keeps the model loaded until it has been idle for <see cref="IdleUnload"/>.</summary>
    void Keep()
    {
        lock (_keepLock)
        {
            if (_disposed) return;
            _kept = true;
            _unload ??= new Timer(_ => Unload());
            _unload.Change(IdleUnload, Timeout.InfiniteTimeSpan);
        }
    }

    void Unload()
    {
        lock (_lock)
        {
            _kept = false;
            Free();
        }
    }

    void Free()
    {
        _tts?.Dispose();
        _tts = null;
    }

    public void Dispose()
    {
        lock (_keepLock)
        {
            _disposed = true;
            _unload?.Dispose();
        }
        lock (_lock) Free();
    }
}
