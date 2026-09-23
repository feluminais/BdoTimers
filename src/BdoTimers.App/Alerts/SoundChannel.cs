using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BdoTimers.App.Alerts;

public sealed class SoundChannel
{
    /// <summary>Plays a file, or the built-in two-tone chime when <paramref name="filePath"/> is null or missing. Completes when playback ends.</summary>
    public Task PlayAsync(string? filePath, float volume)
    {
        ISampleProvider source;
        IDisposable? reader = null;
        if (filePath is not null && File.Exists(filePath))
        {
            var file = new AudioFileReader(filePath);
            reader = file;
            source = file;
        }
        else
        {
            source = Chime();
        }

        var output = new WaveOut();
        output.Init(new VolumeSampleProvider(source) { Volume = Math.Clamp(volume, 0f, 1f) });
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) =>
        {
            output.Dispose();
            reader?.Dispose();
            if (e.Exception is not null) done.TrySetException(e.Exception);
            else done.TrySetResult();
        };
        output.Play();
        return done.Task;
    }

    static ISampleProvider Chime()
    {
        static ISampleProvider Tone(double hz, int ms) =>
            new SignalGenerator(44100, 1) { Frequency = hz, Type = SignalGeneratorType.Sin, Gain = 0.35 }
                .Take(TimeSpan.FromMilliseconds(ms));
        return new ConcatenatingSampleProvider([Tone(880, 160), Tone(1320, 240), Tone(880, 160), Tone(1320, 240)]);
    }
}
