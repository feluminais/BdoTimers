using System.IO;
using System.Windows;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Sounds;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BdoTimers.App.Alerts;

public sealed class SoundChannel(UserSounds userSounds)
{
    readonly Dictionary<string, byte[]> _builtIns = [];

    /// <summary>
    /// Plays a sound key: the bundled WAV for a built-in key, the file for a user sound. A user sound that fails to
    /// open plays the built-in default instead. Completes when playback ends or is cancelled.
    /// </summary>
    public Task PlayAsync(string key, float volume, CancellationToken cancel = default) => PlayAsync(Open(key), volume, cancel);

    /// <summary>Plays any audio (e.g. generated speech) the same way, then disposes it.</summary>
    public Task PlayAsync(WaveStream source, float volume, CancellationToken cancel = default)
    {
        var output = new WaveOut();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stop = cancel.Register(output.Stop);
        output.PlaybackStopped += (_, e) =>
        {
            stop.Dispose();
            output.Dispose();
            source.Dispose();
            if (e.Exception is not null) done.TrySetException(e.Exception);
            else done.TrySetResult();
        };
        try
        {
            output.Init(new VolumeSampleProvider(source.ToSampleProvider()) { Volume = Math.Clamp(volume, 0f, 1f) });
            output.Play();
        }
        catch
        {
            stop.Dispose();
            output.Dispose();
            source.Dispose();
            throw;
        }
        return done.Task;
    }

    /// <summary>True when the file decodes as audio; checked before a file is added to the user's sounds.</summary>
    public static bool CanDecode(string path)
    {
        try
        {
            using var reader = new AudioFileReader(path);
            return reader.TotalTime > TimeSpan.Zero;
        }
        // Decoders fail with many exception types (COM, format, IO); any of them means the file is unusable.
        catch (Exception)
        {
            return false;
        }
    }

    WaveStream Open(string key)
    {
        if (SoundKeys.IsBuiltIn(key)) return BuiltIn(key);
        var path = userSounds.PathFor(key);
        try { return new AudioFileReader(path); }
        catch (Exception ex)
        {
            Log.Error($"Sound file unreadable, playing the built-in default instead: {path}", ex);
            return BuiltIn(BuiltInSounds.Default);
        }
    }

    /// <summary>The bundled WAV, read once per key; alerts can play from several threads.</summary>
    WaveStream BuiltIn(string key)
    {
        byte[] bytes;
        lock (_builtIns)
        {
            if (!_builtIns.TryGetValue(key, out bytes!))
            {
                using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Sounds/{key}.wav"))!.Stream;
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                _builtIns[key] = bytes = copy.ToArray();
            }
        }
        return new WaveFileReader(new MemoryStream(bytes));
    }
}
