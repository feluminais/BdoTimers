using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class JsonFileStoreTests
{
    [Fact]
    public void Missing_file_loads_default_without_recovery()
    {
        using var dir = new TempDir();
        var store = new JsonFileStore<AppSettings>(dir.File("settings.json"), () => new AppSettings { Volume = 0.5f });

        var result = store.Load();

        Assert.Equal(0.5f, result.Value.Volume);
        Assert.Null(result.RecoveredBackupPath);
    }

    [Fact]
    public void Save_then_load_round_trips_and_leaves_no_temp_file()
    {
        using var dir = new TempDir();
        var store = new JsonFileStore<AppSettings>(dir.File("settings.json"), () => new AppSettings());

        store.Save(new AppSettings { TtsRate = 3 });

        Assert.Equal(3, store.Load().Value.TtsRate);
        Assert.Equal(new[] { "settings.json" }, Directory.GetFiles(dir.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    public void Corrupt_file_is_backed_up_and_defaults_load(string content)
    {
        using var dir = new TempDir();
        var path = dir.File("timers.json");
        File.WriteAllText(path, content);
        var store = new JsonFileStore<AppData>(path, () => new AppData());

        var result = store.Load();

        Assert.Empty(result.Value.Timers);
        Assert.NotNull(result.RecoveredBackupPath);
        Assert.Equal(content, File.ReadAllText(result.RecoveredBackupPath!));
        Assert.False(File.Exists(path));
    }
}
