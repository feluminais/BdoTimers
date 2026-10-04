using System.Text.Json;
using System.Text.Json.Serialization;
using BdoTimers.Core.Json;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public class JsonRoundTripTests
{
    // Every field is set, even where a real timer or list would leave some empty, so each one is round-tripped.
    static readonly AppData Data = new()
    {
        DataVersion = DataMigrations.Current,
        SelectedBossRegion = BossRegions.NorthAmerica,
        BossSelectionVersion = Guid.NewGuid(),
        BossAlertsAfterUtc = T0,
        SeedApplied = true,
        AcceptedBossTimetable = SeedService.LoadEmbedded(),
        BossRegions = [new BossRegionState
        {
            RegionId = BossRegions.NorthAmerica, SeedApplied = true,
            AcceptedBossTimetable = SeedService.LoadEmbedded(BossRegions.NorthAmerica), TimetableNoticeRevision = "na-review",
        }],
        Timers =
        [
            new TimerDef
            {
                Name = "Horse registration 3",
                Kind = TimerKind.Countdown,
                Enabled = false,
                IsBuiltIn = true,
                AddedByUser = true,
                BossRegionId = BossRegions.NorthAmerica,
                Scheduled = new ScheduledSpec
                {
                    TimeZoneId = "Europe/Berlin", Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(0, 15), "Battle")],
                    StartDate = new(2026, 9, 1), EndDate = new(2026, 12, 31), EveryWeeks = 2, WeekAnchor = new(2026, 9, 20),
                },
                OneTime = new OneTimeSpec { Date = new(2026, 10, 3), Time = new(20, 0), TimeZoneId = "Europe/Berlin", Finished = true },
                Countdown = new CountdownSpec
                {
                    Duration = TimeSpan.FromMinutes(90), Status = CountdownStatus.Running, EndsAtUtc = T0,
                    Remaining = TimeSpan.FromMinutes(-30), StartedAtUtc = T0.AddMinutes(-90),
                },
                Stopwatch = new StopwatchSpec { Status = CountdownStatus.Paused, StartedAtUtc = T0, Elapsed = TimeSpan.FromMinutes(75) },
                Alerts = new AlertConfig
                {
                    LeadTimesMinutes = [5, 0],
                    Sound = new SoundAlert { Enabled = false, Key = "horn.mp3" },
                    Toast = new ToastAlert { Enabled = false },
                    Tts = new TtsAlert { Enabled = false, Template = "{name} soon", NowTemplate = "{name}!" },
                    Overlay = new OverlayAlert { Enabled = true, ShowMinutesBefore = 10 },
                },
                ControlHotkey = new Hotkey(HotkeyModifiers.Alt, 0x43),
                StartHotkey = new Hotkey(HotkeyModifiers.Ctrl, 0x48),
                HorseRunNumber = 3,
                ImageFile = "horse.png",
                Preset = Presets.HorseRegistrationRun,
            },
        ],
        Muted = [new MutedOccurrence(Guid.NewGuid(), T0)],
    };

    static readonly AppSettings Settings = new()
    {
        Autostart = true,
        CloseToTray = true,
        TextScale = 1.25,
        Volume = 0.5f,
        AlertSound = BuiltInSounds.Chimes,
        TtsVoice = "af_heart",
        TtsRate = 2,
        DefaultLeadTimesMinutes = [10, 0],
        OverlayLeft = 12,
        OverlayTop = 40,
        Overlay = new OverlaySettings
        {
            Enabled = false,
            AlwaysShow = true,
            AlwaysShowHotkey = new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x4F),
            ShowOnHotkey = false,
            ShowHotkey = new Hotkey(HotkeyModifiers.Alt, 0x78),
            ShowSeconds = 30,
            MouseProximity = OverlayMouseProximity.Hide,
            Layout = OverlayLayout.Card,
            ShowOutline = false,
            Scale = 1.5,
            ShowClock = false,
            ShowServerTime = true,
            ShowGameTime = true,
            ShowPrevious = false,
            ShowNext = false,
            ShowFarm = false,
            ShowFishing = false,
            ShowHorseRegistrations = true,
            ShowCustomTimers = false,
            GuildBosses = new OverlayAlert { Enabled = true, ShowMinutesBefore = 30 },
            BackgroundColor = "#3A1417",
            BackgroundImage = "bg.png",
            BackgroundOpacity = 0.4,
            TextOpacity = 0.9,
        },
        AlertsPausedUntilUtc = T0,
        NotificationHintShown = true,
        TimetableNoticeRevision = "eu-review",
        Window = new WindowPlacement(10, 20, 960, 720),
        DailyTodoReset = new TodoSchedule { Day = DayOfWeek.Monday, Hour = 5, Minute = 30, LocalTime = true },
        WeeklyTodoReset = new TodoSchedule { Day = DayOfWeek.Friday, Hour = 12, Minute = 15, LocalTime = true },
        Calendar = new CalendarSettings { ShowBosses = false, ShowTimers = false, ShowEvents = false, ShowResets = false },
    };

    static readonly TodoData Todos = new()
    {
        DefaultsVersion = TodoData.CurrentDefaultsVersion,
        Lists =
        [
            new TodoList
            {
                Name = "Weekly quests", Cadence = TodoCadence.Weekly, IsBuiltIn = true, Enabled = true, Deleted = true,
                NextResetUtc = T0,
                Rows = [new TodoRow { Text = "Olvia Academy", Done = true, Children = [new TodoRow { Text = "Quest 1", Done = true }] }],
            },
        ],
    };

    [Fact]
    public void Timer_data_round_trips() => Assert.Contains("\"Monday\"", AssertRoundTrips(Data));

    [Fact]
    public void Settings_round_trip() => AssertRoundTrips(Settings);

    [Fact]
    public void To_do_data_round_trips() => AssertRoundTrips(Todos);

    [Fact]
    public void Files_without_new_fields_load()
    {
        const string timers = """
            { "timers": [ { "name": "Farm", "kind": "Countdown",
                            "countdown": { "duration": "01:00:00", "status": "Running",
                                           "endsAtUtc": "2026-09-22T12:00:00+00:00" } } ] }
            """;
        const string settings = """{ "volume": 0.5 }""";

        var data = JsonSerializer.Deserialize<AppData>(timers, JsonDefaults.Options)!;
        var s = JsonSerializer.Deserialize<AppSettings>(settings, JsonDefaults.Options)!;

        Assert.Null(data.Timers.Single().Countdown!.StartedAtUtc);
        Assert.Null(data.Timers.Single().ImageFile);
        Assert.Null(s.Window);
        Assert.Equal(BuiltInSounds.Default, s.AlertSound);
        Assert.False(s.Autostart);
        Assert.Equal(0.5f, s.Volume);
    }

    [Fact]
    public void Retired_fields_are_ignored()
    {
        var data = JsonSerializer.Deserialize<AppData>("""{ "seedApplied": true, "dataVersion": 5 }""", JsonDefaults.Options)!;
        var todos = JsonSerializer.Deserialize<TodoData>(
            """{ "lists": [ { "name": "Mine", "cadence": "Weekly", "schedule": { "cadence": "Weekly", "day": "Monday" } } ] }""",
            JsonDefaults.Options)!;
        var settings = JsonSerializer.Deserialize<AppSettings>(
            """{ "weeklyTodoReset": { "cadence": "Weekly", "day": "Monday", "hour": 5 } }""", JsonDefaults.Options)!;

        Assert.Equal(5, data.DataVersion);
        Assert.Equal(TodoCadence.Weekly, todos.Lists.Single().Cadence);
        Assert.Equal(new TodoSchedule { Day = DayOfWeek.Monday, Hour = 5 }, settings.WeeklyTodoReset);
    }

    [Fact]
    public void Settings_without_overlay_load_the_defaults_and_keep_the_position()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("""{ "overlayLeft": 100, "overlayTop": 40 }""", JsonDefaults.Options)!;

        Assert.Equal(new OverlaySettings(), s.Overlay);
        Assert.True(s.Overlay.Enabled);
        Assert.False(s.Overlay.AlwaysShow);
        Assert.False(s.Overlay.GuildBosses.Enabled);
        Assert.Equal(100, s.OverlayLeft);
        Assert.Equal(40, s.OverlayTop);
    }

    [Fact]
    public void Explicitly_cleared_hotkeys_stay_cleared()
    {
        const string json = """{ "overlay": { "alwaysShowHotkey": null, "showOnHotkey": false, "showHotkey": null } }""";
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options)!;
        Assert.Null(settings.Overlay.AlwaysShowHotkey);
        Assert.Null(settings.Overlay.ShowHotkey);
        Assert.False(settings.Overlay.ShowOnHotkey);
    }

    /// <summary>Serializes, reads back and serializes again; the same text means every field came back.</summary>
    static string AssertRoundTrips<T>(T sample) where T : class
    {
        AssertPopulated(sample, []);
        var json = JsonSerializer.Serialize(sample, JsonDefaults.Options);
        var back = JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
        Assert.Equal(json, JsonSerializer.Serialize(back, JsonDefaults.Options));
        return json;
    }

    /// <summary>
    /// Fails for a property still at its default in <paramref name="sample"/>, or in the first instance of each model type
    /// inside it, so a field added later has to be set in these samples and is round-tripped from then on.
    /// </summary>
    static void AssertPopulated(object sample, HashSet<Type> checkedTypes)
    {
        var type = sample.GetType();
        if (!checkedTypes.Add(type)) return;
        var defaults = type.GetConstructor(Type.EmptyTypes) is not null ? Activator.CreateInstance(type) : null;
        foreach (var property in type.GetProperties().Where(p => p.SetMethod is not null
            && p.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).OfType<JsonIgnoreAttribute>().All(a => a.Condition != JsonIgnoreCondition.Always)))
        {
            var value = property.GetValue(sample);
            var initial = defaults is not null ? property.GetValue(defaults)
                : property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null;
            Assert.False(Equals(value, initial), $"{type.Name}.{property.Name} keeps its default");
            if (value is IEnumerable<object> items) value = items.FirstOrDefault();
            if (value?.GetType() is { IsEnum: false, Namespace: "BdoTimers.Core.Model" }) AssertPopulated(value, checkedTypes);
        }
    }
}
