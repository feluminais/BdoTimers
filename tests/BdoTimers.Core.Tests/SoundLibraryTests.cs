using BdoTimers.Core.Model;
using BdoTimers.Core.Sounds;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class SoundLibraryTests
{
    static Func<string, bool> Only(params string[] present) => key => present.Contains(key);

    [Theory]
    [InlineData("horn", "gong", "horn")]
    [InlineData("mine.mp3", "gong", "mine.mp3")]
    [InlineData(null, "bell", "bell")]
    [InlineData(null, "mine.mp3", "mine.mp3")]
    [InlineData("gone.wav", "bell", "bell")]
    [InlineData("organ", "bell", "bell")]
    [InlineData(null, "gone.wav", BuiltInSounds.Default)]
    [InlineData("gone.wav", "organ", BuiltInSounds.Default)]
    public void Playable_falls_back_from_the_timer_to_the_app_sound_to_the_built_in_default(
        string? key, string appDefault, string expected) =>
        Assert.Equal(expected, SoundKeys.Playable(key, appDefault, Only("mine.mp3")));

    [Theory]
    [InlineData("gong", false)]
    [InlineData("mine.mp3", true)]
    [InlineData("organ", false)]
    [InlineData(@"C:\sounds\mine.mp3", false)]
    public void User_keys_are_bare_file_names(string key, bool expected) =>
        Assert.Equal(expected, SoundKeys.IsUserKey(key));

    [Fact]
    public void Import_copies_under_the_original_name_and_numbers_clashes()
    {
        using var source = new TempDir();
        using var dir = new TempDir();
        var file = source.File("Boss Horn.MP3");
        File.WriteAllText(file, "x");
        var sounds = new UserSounds(dir.File("sounds"));

        var first = sounds.Import(file);
        var second = sounds.Import(file);

        Assert.Equal("Boss Horn.MP3", first);
        Assert.Equal("Boss Horn (2).MP3", second);
        Assert.True(File.Exists(sounds.PathFor(second)));
        Assert.True(File.Exists(file));
        Assert.Equal(["Boss Horn (2).MP3", "Boss Horn.MP3"], sounds.Keys());
    }

    [Fact]
    public void Import_refuses_other_file_types()
    {
        using var dir = new TempDir();
        var file = dir.File("notes.txt");
        File.WriteAllText(file, "x");

        Assert.Throws<ArgumentException>(() => new UserSounds(dir.File("sounds")).Import(file));
    }

    [Fact]
    public void Keys_lists_only_sound_files_and_is_empty_without_a_folder()
    {
        using var dir = new TempDir();
        var sounds = new UserSounds(dir.File("sounds"));
        Assert.Empty(sounds.Keys());

        Directory.CreateDirectory(dir.File("sounds"));
        File.WriteAllText(dir.File(@"sounds\b.wav"), "x");
        File.WriteAllText(dir.File(@"sounds\a.mp3"), "x");
        File.WriteAllText(dir.File(@"sounds\readme.txt"), "x");

        Assert.Equal(["a.mp3", "b.wav"], sounds.Keys());
        Assert.True(sounds.Exists("a.mp3"));
        Assert.False(sounds.Exists("readme.txt"));
        Assert.False(sounds.Exists("gong"));
    }

    [Fact]
    public void Delete_removes_the_copy()
    {
        using var dir = new TempDir();
        var source = dir.File("horn.wav");
        File.WriteAllText(source, "x");
        var sounds = new UserSounds(dir.File("sounds"));
        var key = sounds.Import(source);

        sounds.Delete(key);

        Assert.Empty(sounds.Keys());
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void ForgetSound_sets_timers_using_it_back_to_default()
    {
        using var dir = new TempDir();
        var file = new JsonFileStore<AppData>(dir.File("timers.json"), () => new AppData());
        var store = new TimerStore(file, new AppData());
        TimerDef With(string name, SoundAlert sound) => new() { Name = name, Alerts = new AlertConfig { Sound = sound } };
        store.Upsert(With("A", new SoundAlert { Key = "horn.wav" }));
        store.Upsert(With("B", new SoundAlert { Key = "bell" }));
        store.Upsert(With("C", new SoundAlert { Enabled = false }));

        store.ForgetSound("horn.wav");

        var sounds = store.Current.Timers.ToDictionary(t => t.Name, t => t.Alerts.Sound);
        Assert.Equal(new SoundAlert(), sounds["A"]);
        Assert.Equal(new SoundAlert { Key = "bell" }, sounds["B"]);
        Assert.Equal(new SoundAlert { Enabled = false }, sounds["C"]);
    }
}
