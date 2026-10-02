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

    [Fact]
    public void Save_replaces_an_existing_file()
    {
        using var dir = new TempDir();
        var store = new JsonFileStore<AppSettings>(dir.File("settings.json"), () => new AppSettings());
        store.Save(new AppSettings { TtsRate = 3 });

        store.Save(new AppSettings { TtsRate = 5 });

        Assert.Equal(5, store.Load().Value.TtsRate);
        Assert.Equal(new[] { "settings.json" }, Directory.GetFiles(dir.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("{\"timers\":null}")]
    [InlineData("{\"timers\":[null]}")]
    [InlineData("{\"bossRegions\":null}")]
    [InlineData("{\"dataVersion\":999}")]
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

    [Theory]
    [InlineData("{\"overlay\":null}")]
    [InlineData("{\"dailyTodoReset\":null}")]
    [InlineData("{\"defaultLeadTimesMinutes\":null}")]
    public void Invalid_settings_are_preserved_and_replaced_with_defaults(string content)
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        File.WriteAllText(path, content);
        var defaults = new AppSettings();

        var result = new JsonFileStore<AppSettings>(path, () => defaults).Load();

        Assert.Same(defaults, result.Value);
        Assert.Equal(content, File.ReadAllText(result.RecoveredBackupPath!));
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("{\"lists\":null}")]
    [InlineData("{\"lists\":[null]}")]
    [InlineData("{\"lists\":[{\"rows\":null}]}")]
    public void Invalid_todo_data_is_preserved_and_replaced_with_defaults(string content)
    {
        using var dir = new TempDir();
        var path = dir.File("todos.json");
        File.WriteAllText(path, content);

        var result = new JsonFileStore<TodoData>(path, () => new()).Load();

        Assert.Empty(result.Value.Lists);
        Assert.Equal(content, File.ReadAllText(result.RecoveredBackupPath!));
    }

    [Fact]
    public void Valid_legacy_timer_data_still_loads_before_migration()
    {
        using var dir = new TempDir();
        var path = dir.File("timers.json");
        File.WriteAllText(path, "{\"seedApplied\":true,\"timers\":[{\"name\":\"My timer\",\"kind\":\"Countdown\",\"countdown\":{}}]}");

        var result = new JsonFileStore<AppData>(path, () => new()).Load();

        Assert.Null(result.RecoveredBackupPath);
        Assert.Equal(0, result.Value.DataVersion);
        Assert.Equal("My timer", Assert.Single(result.Value.Timers).Name);
        Assert.True(File.Exists(path));
    }
}
