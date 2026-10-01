using System.IO;
using System.Security.Cryptography;
using System.Text;
using BdoTimers.Core.Diagnostics;
using NAudio.Wave;

namespace BdoTimers.App.Alerts;

/// <summary>
/// Generated speech kept as WAV files in <paramref name="dir"/>, so a line is synthesized once and later alerts only read
/// it. Keys cover everything that shapes the audio, so a new model, voice, speed or wording gets its own entry; entries
/// unused for a month are deleted by <see cref="DeleteUnused"/>.
/// </summary>
public sealed class SpeechCache(string dir)
{
    static readonly TimeSpan Unused = TimeSpan.FromDays(30);

    /// <summary>The file name for speech made from <paramref name="parts"/>.</summary>
    public static string Key(params string[] parts) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));

    public bool Contains(string key) => File.Exists(PathOf(key));

    /// <summary>The stored speech, or null when there is none or it can't be read. Reading it counts as use.</summary>
    public WaveStream? Open(string key)
    {
        var path = PathOf(key);
        if (!File.Exists(path)) return null;
        try
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return new WaveFileReader(new MemoryStream(File.ReadAllBytes(path)));
        }
        // Reading fails with IO or format exceptions; either way the line is synthesized again and stored over it.
        catch (Exception ex)
        {
            Log.Error($"Cached speech unreadable: {path}", ex);
            return null;
        }
    }

    /// <summary>Stores mono samples; written aside and moved into place, so a reader never sees half a file.</summary>
    public void Store(string key, float[] samples, int sampleRate)
    {
        Directory.CreateDirectory(dir);
        var path = PathOf(key);
        var partial = path + ".partial";
        using (var writer = new WaveFileWriter(partial, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
            writer.WriteSamples(samples, 0, samples.Length);
        File.Move(partial, path, overwrite: true);
    }

    /// <summary>Deletes entries, and partial files left by a crash, not used for a month.</summary>
    public void DeleteUnused()
    {
        if (!Directory.Exists(dir)) return;
        var cutoff = DateTime.UtcNow - Unused;
        foreach (var file in new DirectoryInfo(dir).EnumerateFiles().Where(f => f.LastWriteTimeUtc < cutoff))
        {
            try { file.Delete(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error($"Couldn't delete cached speech {file.FullName}", ex);
            }
        }
    }

    string PathOf(string key) => Path.Combine(dir, key + ".wav");
}
