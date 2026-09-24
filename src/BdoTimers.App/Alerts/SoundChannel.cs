using System.IO;
using System.Windows;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BdoTimers.App.Alerts;

public sealed class SoundChannel
{
    readonly Dictionary<string, byte[]> _builtIns = [];

    /// <summary>
    /// Plays <paramref name="filePath"/>, or the built-in <paramref name="builtIn"/> sound when the path is null,
    /// missing or unreadable. Completes when playback ends.
    /// </summary>
    public Task PlayAsync(string? filePath, string builtIn, float volume)
    {
        var file = OpenFile(filePath);
        WaveStream source = (WaveStream?)file ?? new WaveFileReader(new MemoryStream(BuiltIn(builtIn)));
        var output = new WaveOut();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) =>
        {
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
            output.Dispose();
            source.Dispose();
            throw;
        }
        return done.Task;
    }

    static AudioFileReader? OpenFile(string? filePath)
    {
        if (filePath is null || !File.Exists(filePath)) return null;
        try { return new AudioFileReader(filePath); }
        catch (Exception ex)
        {
            Log.Error($"Sound file unreadable, playing the alert sound instead: {filePath}", ex);
            return null;
        }
    }

    /// <summary>The bundled WAV bytes, read once per key; alerts can play from several threads.</summary>
    byte[] BuiltIn(string key)
    {
        key = BuiltInSounds.Resolve(key);
        lock (_builtIns)
        {
            if (_builtIns.TryGetValue(key, out var cached)) return cached;
            using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Sounds/{key}.wav"))!.Stream;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return _builtIns[key] = copy.ToArray();
        }
    }
}
