using System.IO;
using BdoTimers.Core.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BdoTimers.App.Alerts;

public sealed class SoundChannel
{
    /// <summary>Plays a file, or the built-in two-tone chime when <paramref name="filePath"/> is null, missing or unreadable. Completes when playback ends.</summary>
    public Task PlayAsync(string? filePath, float volume)
    {
        var file = OpenFile(filePath);
        var output = new WaveOut();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) =>
        {
            output.Dispose();
            file?.Dispose();
            if (e.Exception is not null) done.TrySetException(e.Exception);
            else done.TrySetResult();
        };
        try
        {
            output.Init(new VolumeSampleProvider(file ?? Chime()) { Volume = Math.Clamp(volume, 0f, 1f) });
            output.Play();
        }
        catch
        {
            output.Dispose();
            file?.Dispose();
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
            Log.Error($"Sound file unreadable, playing chime: {filePath}", ex);
            return null;
        }
    }

    static ISampleProvider Chime()
    {
        static ISampleProvider Tone(double hz, int ms) =>
            new SignalGenerator(44100, 1) { Frequency = hz, Type = SignalGeneratorType.Sin, Gain = 0.35 }
                .Take(TimeSpan.FromMilliseconds(ms));
        return new ConcatenatingSampleProvider([Tone(880, 160), Tone(1320, 240), Tone(880, 160), Tone(1320, 240)]);
    }
}
