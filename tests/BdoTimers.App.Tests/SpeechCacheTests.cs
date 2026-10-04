using BdoTimers.App.Alerts;
using System.IO;

namespace BdoTimers.App.Tests;

public class SpeechCacheTests
{
    [Fact]
    public void CleanupKeepsSavedLinesEvenWhenTheyHaveNotPlayedForMonths()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var cache = new SpeechCache(root);
            cache.Store("saved", [0f], 24000);
            File.SetLastWriteTimeUtc(Path.Combine(root, "saved.wav"), DateTime.UtcNow.AddDays(-90));
            cache.DeleteUnused();
            Assert.True(cache.Contains("saved"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void BundledLinesCanBeReadWithoutCreatingPersonalCache()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var bundled = Path.Combine(root, "bundled");
            new SpeechCache(bundled).Store("line", [0f, 0.1f], 24000);
            var personal = Path.Combine(root, "personal");
            var cache = new SpeechCache(personal, bundled);
            Assert.True(cache.Contains("line"));
            using var audio = cache.Open("line");
            Assert.NotNull(audio);
            Assert.False(Directory.Exists(personal));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ModelIdentitySurvivesCopyingWithDifferentTimestamps()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, "model.int8.onnx"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(root, "voices.bin"), [4, 5, 6]);
            var first = new KokoroEngine(root).ModelIdentity;
            File.SetLastWriteTimeUtc(Path.Combine(root, "model.int8.onnx"), DateTime.UtcNow.AddDays(-1));
            Assert.Equal(first, new KokoroEngine(root).ModelIdentity);
            File.WriteAllBytes(Path.Combine(root, "voices.bin"), [4, 5, 7]);
            Assert.NotEqual(first, new KokoroEngine(root).ModelIdentity);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
