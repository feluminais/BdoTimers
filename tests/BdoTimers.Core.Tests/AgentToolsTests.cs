using System.Text.Json.Nodes;
using BdoTimers.Core.Agent;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using static BdoTimers.Core.Tests.TestTimes;

namespace BdoTimers.Core.Tests;

public sealed class AgentToolsTests : IDisposable
{
    readonly TempDir _dir = new();
    readonly FakeClock _clock = new(T0);
    readonly TimerStore _timers;
    readonly TodoStore _todos;
    readonly PersistentState<AppSettings> _settings;
    readonly List<Guid> _deleted = [];
    readonly List<Guid> _deletedBosses = [];
    readonly AgentTools _tools;

    public AgentToolsTests()
    {
        _timers = new TimerStore(new JsonFileStore<AppData>(_dir.File("timers.json"), () => new AppData()), new AppData());
        _todos = new TodoStore(new JsonFileStore<TodoData>(_dir.File("todos.json"), () => new TodoData()), new TodoData(), _clock);
        _settings = new PersistentState<AppSettings>(new JsonFileStore<AppSettings>(_dir.File("settings.json"), () => new AppSettings()), new AppSettings());
        _tools = new AgentTools(_timers, _todos, _settings, _clock, TimeZoneInfo.Utc,
            new AgentHooks(DeleteTimer: id => { _deleted.Add(id); _timers.Delete(id); return true; },
                DeleteBoss: id => { _deletedBosses.Add(id); _timers.Delete(id); return true; }));
    }

    public void Dispose() => _dir.Dispose();

    JsonObject Call(string tool, string args = "{}") => _tools.Call(tool, JsonNode.Parse(args)!.AsObject()).AsObject();

    static string Message(Action action) => Assert.Throws<AgentToolException>(action).Message;

    TimerDef Add(TimerDef timer)
    {
        _timers.Upsert(timer);
        return timer;
    }

    static TimerDef Countdown(string name = "Brew", int minutes = 30) => new()
    {
        Name = name, Kind = TimerKind.Countdown, Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(minutes) },
    };

    static string[] Names(JsonNode array, string field = "name") => array.AsArray().Select(n => n![field]!.GetValue<string>()).ToArray();

    TodoList AddList(string name, bool enabled = true, params TodoRow[] rows)
    {
        var list = new TodoList { Name = name, Enabled = enabled, Cadence = TodoCadence.Daily, NextResetUtc = T0.AddHours(6), Rows = rows };
        _todos.Update(d => d with { Lists = [.. d.Lists, list] });
        return list;
    }

    // ---- Reading

    [Fact]
    public void Every_listed_tool_is_run_by_Call()
    {
        foreach (var tool in AgentCatalog.Tools)
        {
            try { _tools.Call(tool.Name, new JsonObject()); }
            catch (AgentToolException ex) { Assert.DoesNotContain("Unknown tool", ex.Message); }
        }
        Assert.Equal(AgentCatalog.Tools.Count, AgentCatalog.Tools.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void Unknown_tool_is_refused()
    {
        Assert.Contains("Unknown tool", Message(() => _tools.Call("format_disk", new JsonObject())));
    }

    [Fact]
    public void Status_gives_the_game_clock_and_running_timers()
    {
        Add(Countdown() with { Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(30), Status = CountdownStatus.Running, EndsAtUtc = T0.AddMinutes(12) } });
        Add(Countdown("Idle one"));

        var status = Call("get_status");

        Assert.Equal("2026-09-22T12:00:00Z", status["nowUtc"]!.GetValue<string>());
        // 12:00 UTC is 220 minutes after dawn at 00:20: night, with 20 minutes left.
        Assert.True(status["game"]!["isNight"]!.GetValue<bool>());
        Assert.Equal(20, status["game"]!["dayBreaksInMinutes"]!.GetValue<int>());
        Assert.False(status["alerts"]!["paused"]!.GetValue<bool>());
        var running = Assert.Single(status["running"]!.AsArray());
        Assert.Equal("Brew", running!["name"]!.GetValue<string>());
        Assert.Equal(12, running["next"]!["inMinutes"]!.GetValue<int>());
    }

    [Fact]
    public void List_timers_leaves_bosses_out_unless_asked()
    {
        Add(Countdown());
        Add(TestTimers.Boss("Kzarka", DayOfWeek.Tuesday, 20));

        Assert.Equal(["Brew"], Names(Call("list_timers")["timers"]!));
        Assert.Equal(["Brew", "Kzarka"], Names(Call("list_timers", """{"bosses":true}""")["timers"]!));
    }

    [Fact]
    public void List_timers_filters_by_kind_and_name_and_hides_other_regions_bosses()
    {
        Add(Countdown("Alpha"));
        Add(new TimerDef { Name = "Alpha watch", Kind = TimerKind.Stopwatch, Stopwatch = new StopwatchSpec() });
        Add(TestTimers.Boss("Kzarka", DayOfWeek.Tuesday, 20) with { BossRegionId = BossRegions.NorthAmerica });

        Assert.Equal(["Alpha watch"], Names(Call("list_timers", """{"kind":"stopwatch"}""")["timers"]!));
        Assert.Equal(["Alpha", "Alpha watch"], Names(Call("list_timers", """{"query":"alpha"}""")["timers"]!));
        Assert.Empty(Call("list_timers", """{"bosses":true,"query":"kzarka"}""")["timers"]!.AsArray());
        Assert.Contains("kind must be", Message(() => Call("list_timers", """{"kind":"clock"}""")));
    }

    [Fact]
    public void A_weekly_timer_shows_its_schedule_and_next_time()
    {
        Add(new TimerDef
        {
            Name = "Raid", Kind = TimerKind.Scheduled,
            Scheduled = new ScheduledSpec { TimeZoneId = "UTC", Slots = [new Slot(DayOfWeek.Tuesday, new TimeOnly(20, 0), "Boss")] },
        });

        var raid = Call("list_timers")["timers"]![0]!;

        Assert.Equal("Tue 20:00 Boss (UTC)", raid["schedule"]!.GetValue<string>());
        Assert.Equal("2026-09-22T20:00:00Z", raid["next"]!["utc"]!.GetValue<string>());
        Assert.Equal(480, raid["next"]!["inMinutes"]!.GetValue<int>());
        Assert.Equal("Raid · Boss", raid["next"]!["title"]!.GetValue<string>());
    }

    [Fact]
    public void A_disabled_timer_has_no_next_time()
    {
        Add(Countdown() with { Enabled = false, Countdown = new CountdownSpec { Status = CountdownStatus.Running, EndsAtUtc = T0.AddMinutes(5) } });

        Assert.Null(Call("list_timers")["timers"]![0]!["next"]);
    }

    [Fact]
    public void Upcoming_is_in_time_order_and_skips_off_and_skipped_ones()
    {
        var late = Add(TestTimers.Boss("Late", DayOfWeek.Tuesday, 22));
        Add(TestTimers.Boss("Soon", DayOfWeek.Tuesday, 15));
        Add(TestTimers.Boss("Off", DayOfWeek.Tuesday, 16) with { Enabled = false });
        var skipped = Add(TestTimers.Boss("Skipped", DayOfWeek.Tuesday, 17));
        _timers.ToggleMute(skipped.Id, new DateTimeOffset(2026, 9, 22, 15, 0, 0, TimeSpan.Zero));
        Add(Countdown("Brew") with { Countdown = new CountdownSpec { Status = CountdownStatus.Running, EndsAtUtc = T0.AddMinutes(90) } });

        var upcoming = Call("get_upcoming")["upcoming"]!;

        // Berlin is two hours ahead of UTC in September: 15:00 there is 13:00 UTC.
        Assert.Equal(["Soon", "Brew", "Late"], Names(upcoming));
        Assert.Equal(["Brew"], Names(Call("get_upcoming", """{"bosses":false}""")["upcoming"]!));
        Assert.Equal(late.Id.ToString(), upcoming[2]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Upcoming_respects_hours_and_limit()
    {
        Add(TestTimers.Boss("Soon", DayOfWeek.Tuesday, 15));
        Add(TestTimers.Boss("Later", DayOfWeek.Wednesday, 15));

        Assert.Equal(["Soon"], Names(Call("get_upcoming", """{"hours":2}""")["upcoming"]!));
        Assert.Equal(["Soon"], Names(Call("get_upcoming", """{"limit":1}""")["upcoming"]!));
        Assert.Contains("hours must be from 1 to 168", Message(() => Call("get_upcoming", """{"hours":0}""")));
        Assert.Contains("limit must be a whole number", Message(() => Call("get_upcoming", """{"limit":"many"}""")));
    }

    // ---- Timers

    [Fact]
    public void A_countdown_can_be_started_paused_resumed_and_reset()
    {
        var timer = Add(Countdown());

        var started = Call("control_timer", $$"""{"timer":"{{timer.Id}}","action":"start"}""");
        Assert.Equal("running", started["status"]!.GetValue<string>());
        Assert.Equal(1800, started["leftSeconds"]!.GetValue<long>());

        _clock.UtcNow = T0.AddMinutes(10);
        var paused = Call("control_timer", """{"timer":"brew","action":"pause"}""");
        Assert.Equal("paused", paused["status"]!.GetValue<string>());
        Assert.Equal(1200, paused["leftSeconds"]!.GetValue<long>());

        Assert.Equal("running", Call("control_timer", """{"timer":"Brew","action":"resume"}""")["status"]!.GetValue<string>());
        Assert.Equal("idle", Call("control_timer", """{"timer":"Brew","action":"reset"}""")["status"]!.GetValue<string>());
    }

    [Fact]
    public void Control_refuses_a_state_it_cannot_move_from()
    {
        var timer = Add(Countdown());
        var id = timer.Id;

        Assert.Contains("isn't running", Message(() => Call("control_timer", $$"""{"timer":"{{id}}","action":"pause"}""")));
        Assert.Contains("isn't paused", Message(() => Call("control_timer", $$"""{"timer":"{{id}}","action":"resume"}""")));
        Call("control_timer", $$"""{"timer":"{{id}}","action":"start"}""");
        Assert.Contains("is running", Message(() => Call("control_timer", $$"""{"timer":"{{id}}","action":"start"}""")));
        Assert.Contains("action must be", Message(() => Call("control_timer", $$"""{"timer":"{{id}}","action":"explode"}""")));
    }

    [Fact]
    public void A_stopwatch_counts_up_and_a_weekly_timer_cannot_be_controlled()
    {
        Add(new TimerDef { Name = "Fish", Kind = TimerKind.Stopwatch, Stopwatch = new StopwatchSpec() });
        Add(TestTimers.Scheduled("Weekly", DayOfWeek.Monday, 20, 0));

        Call("control_timer", """{"timer":"Fish","action":"start"}""");
        _clock.UtcNow = T0.AddSeconds(90);
        var fish = Call("control_timer", """{"timer":"Fish","action":"pause"}""");

        Assert.Equal(90, fish["elapsedSeconds"]!.GetValue<long>());
        Assert.Equal("1m 30s", fish["elapsed"]!.GetValue<string>());
        Assert.Contains("can be controlled", Message(() => Call("control_timer", """{"timer":"Weekly","action":"start"}""")));
    }

    [Fact]
    public void Reset_goes_through_the_apps_hook_when_it_has_one()
    {
        var reset = new List<Guid>();
        var tools = new AgentTools(_timers, _todos, _settings, _clock, TimeZoneInfo.Utc, new AgentHooks(ResetTimer: id => { reset.Add(id); return true; }));
        var timer = Add(Countdown());

        tools.Call("control_timer", JsonNode.Parse($$"""{"timer":"{{timer.Id}}","action":"reset"}""")!.AsObject());

        Assert.Equal([timer.Id], reset);
    }

    [Fact]
    public void Names_match_exactly_in_any_case_and_a_near_miss_only_suggests()
    {
        var tea = Add(Countdown("Tea"));
        var leaves = Add(Countdown("Tea leaves"));
        var coffee = Add(Countdown("Coffee"));

        Assert.Equal(tea.Id.ToString(), Call("set_timer_enabled", """{"timer":"TEA","enabled":false}""")["id"]!.GetValue<string>());
        Assert.Equal(coffee.Id.ToString(), Call("set_timer_enabled", $$"""{"timer":" {{coffee.Id}} ","enabled":false}""")["id"]!.GetValue<string>());
        var near = Message(() => Call("set_timer_enabled", """{"timer":"tea l","enabled":true}"""));
        Assert.StartsWith("No timer matches 'tea l'.", near);
        Assert.Contains($"Tea leaves ({leaves.Id})", near);
        Assert.DoesNotContain("Coffee", near);
        Assert.Equal("No timer matches 'tomato'.", Message(() => Call("set_timer_enabled", """{"timer":"tomato","enabled":true}""")));
        Assert.True(_timers.Current.Timers.Single(t => t.Id == leaves.Id).Enabled);
    }

    [Fact]
    public void Two_timers_with_one_name_are_told_apart_by_id()
    {
        var first = Add(Countdown("Tea"));
        var second = Add(Countdown("tea"));

        var error = Message(() => Call("delete_timer", """{"timer":"Tea"}"""));

        Assert.StartsWith("More than one timer is named 'Tea'", error);
        Assert.Contains(first.Id.ToString(), error);
        Assert.Contains(second.Id.ToString(), error);
        Assert.Equal(2, _timers.Current.Timers.Count);
    }

    [Fact]
    public void Create_countdown_adds_it_and_can_start_it()
    {
        var made = Call("create_countdown", """{"name":"  Tea ","minutes":5,"start":true}""");

        Assert.Equal("Tea", made["name"]!.GetValue<string>());
        Assert.Equal("running", made["status"]!.GetValue<string>());
        Assert.Equal(300, made["leftSeconds"]!.GetValue<long>());
        var saved = _timers.Current.Timers.Single();
        Assert.Equal(TimeSpan.FromMinutes(5), saved.Countdown!.Duration);
        Assert.Equal(T0.AddMinutes(5), saved.Countdown.EndsAtUtc);
        Assert.Equal("idle", Call("create_countdown", """{"name":"Tea 2","minutes":5}""")["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("""{"name":"Tea","minutes":0}""", "minutes must be from 1 to 1440")]
    [InlineData("""{"name":"Tea","minutes":1441}""", "minutes must be from 1 to 1440")]
    [InlineData("""{"name":"Tea"}""", "minutes is required")]
    [InlineData("""{"name":"   ","minutes":5}""", "name is required")]
    [InlineData("""{"minutes":5}""", "name is required")]
    public void Create_countdown_checks_its_arguments(string args, string error)
    {
        Assert.Contains(error, Message(() => Call("create_countdown", args)));
        Assert.Empty(_timers.Current.Timers);
    }

    [Fact]
    public void Create_event_uses_the_given_zone()
    {
        var made = Call("create_event", """{"name":"Siege","at":"2026-09-23 20:30","timeZone":"Europe/Berlin"}""");

        Assert.Equal("event", made["kind"]!.GetValue<string>());
        Assert.Equal("2026-09-23T18:30:00Z", made["next"]!["utc"]!.GetValue<string>());
        Assert.Equal("Europe/Berlin", _timers.Current.Timers.Single().OneTime!.TimeZoneId);
    }

    [Fact]
    public void Create_event_refuses_a_past_time_a_bad_format_and_an_unknown_zone()
    {
        Assert.Contains("already passed", Message(() => Call("create_event", """{"name":"Old","at":"2026-09-22 11:59"}""")));
        Assert.Contains("must look like", Message(() => Call("create_event", """{"name":"Odd","at":"tomorrow"}""")));
        Assert.Contains("Unknown time zone", Message(() => Call("create_event", """{"name":"Odd","at":"2026-09-23 20:30","timeZone":"Mars/Olympus"}""")));
        Assert.Empty(_timers.Current.Timers);
    }

    [Fact]
    public void A_boss_can_be_turned_off_and_on()
    {
        var boss = Add(TestTimers.Boss("Kzarka", DayOfWeek.Tuesday, 20));

        Assert.False(Call("set_timer_enabled", """{"timer":"Kzarka","enabled":false}""")["enabled"]!.GetValue<bool>());
        Assert.False(_timers.Current.Timers.Single(t => t.Id == boss.Id).Enabled);
        Assert.True(Call("set_timer_enabled", """{"timer":"Kzarka","enabled":"true"}""")["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Delete_removes_a_custom_timer_through_the_hook_and_refuses_built_in_ones()
    {
        var mine = Add(Countdown());
        Add(TestTimers.Boss("Kzarka", DayOfWeek.Tuesday, 20));
        Add(Countdown("Farm") with { Preset = Presets.Farm });

        Assert.Equal("Brew", Call("delete_timer", """{"timer":"Brew"}""")["deleted"]!["name"]!.GetValue<string>());
        Assert.Equal([mine.Id], _deleted);
        Assert.DoesNotContain(_timers.Current.Timers, t => t.Id == mine.Id);
        Assert.Contains("built in", Message(() => Call("delete_timer", """{"timer":"Kzarka"}""")));
        Assert.Contains("built in", Message(() => Call("delete_timer", """{"timer":"Farm"}""")));
        Assert.Equal(2, _timers.Current.Timers.Count);
    }

    // ---- Bosses

    [Fact]
    public void Add_boss_spawns_every_day_in_the_regions_server_time_within_its_dates()
    {
        var made = Call("add_boss", """{"name":" Innocence Mk XII ","times":["23:30","19:30"],"from":"2026-09-22","to":"2026-09-23"}""");

        Assert.True(made["boss"]!.GetValue<bool>());
        Assert.Equal("2026-09-22T17:30:00Z", made["next"]!["utc"]!.GetValue<string>());
        Assert.Equal("2026-09-23", made["to"]!.GetValue<string>());
        var boss = _timers.Current.Timers.Single();
        Assert.Equal("Innocence Mk XII", boss.Name);
        Assert.True(boss.IsBuiltIn && boss.AddedByUser);
        Assert.Equal(BossRegions.Europe, boss.BossRegionId);
        Assert.Equal("Europe/Berlin", boss.Scheduled!.TimeZoneId);
        Assert.Equal(14, boss.Scheduled.Slots.Count);
        Assert.Equal(new Slot(DayOfWeek.Monday, new TimeOnly(19, 30)), boss.Scheduled.Slots[0]);
        Assert.Equal((new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23)), (boss.Scheduled.StartDate!.Value, boss.Scheduled.EndDate!.Value));
        Assert.Equal(["2026-09-22T17:30:00Z", "2026-09-22T21:30:00Z", "2026-09-23T17:30:00Z", "2026-09-23T21:30:00Z"],
            Names(Call("get_upcoming", """{"hours":168}""")["upcoming"]!, "utc"));
        Assert.Equal(["Innocence Mk XII"], Names(Call("list_timers", """{"bosses":true}""")["timers"]!));
    }

    [Fact]
    public void Add_boss_takes_days_and_a_lone_time_for_the_selected_region()
    {
        _timers.Update(d => d with { SelectedBossRegion = BossRegions.NorthAmerica });

        Call("add_boss", """{"name":"Kzarka","times":"13:00","days":["sat","Sunday","SAT"]}""");

        var boss = _timers.Current.Timers.Single();
        Assert.Equal(BossRegions.NorthAmerica, boss.BossRegionId);
        Assert.Equal("America/Los_Angeles", boss.Scheduled!.TimeZoneId);
        Assert.Equal([new Slot(DayOfWeek.Saturday, new TimeOnly(13, 0)), new Slot(DayOfWeek.Sunday, new TimeOnly(13, 0))], boss.Scheduled.Slots);
        Assert.Null(boss.Scheduled.StartDate);
        Assert.Null(boss.Scheduled.EndDate);
    }

    [Theory]
    [InlineData("""{"name":"kzarka","times":["19:30"]}""", "already named")]
    [InlineData("""{"name":"Odd","times":["25:00"]}""", "isn't a time")]
    [InlineData("""{"name":"Odd","times":[]}""", "times is required")]
    [InlineData("""{"name":"Odd","times":[19]}""", "list of text")]
    [InlineData("""{"name":"Odd","times":["19:30"],"days":["Funday"]}""", "isn't a day")]
    [InlineData("""{"name":"Odd","times":["19:30"],"from":"22.10.2026"}""", "from must look like")]
    [InlineData("""{"name":"Odd","times":["19:30"],"from":"2026-11-04","to":"2026-10-22"}""", "End date must be on or after start date")]
    public void Add_boss_refuses_a_taken_name_and_bad_times_days_and_dates(string args, string error)
    {
        Add(TestTimers.Boss("Kzarka", DayOfWeek.Tuesday, 20));

        Assert.Contains(error, Message(() => Call("add_boss", args)));
        Assert.Single(_timers.Current.Timers);
    }

    [Fact]
    public void Delete_removes_an_added_boss_through_the_boss_hook()
    {
        Call("add_boss", """{"name":"Event boss","times":["19:30"]}""");
        var added = _timers.Current.Timers.Single();
        Add(TestTimers.Boss("Kzarka", DayOfWeek.Tuesday, 20));

        Assert.Equal("Event boss", Call("delete_timer", """{"timer":"Event boss"}""")["deleted"]!["name"]!.GetValue<string>());
        Assert.Equal([added.Id], _deletedBosses);
        Assert.Empty(_deleted);
        Assert.Contains("built in", Message(() => Call("delete_timer", """{"timer":"Kzarka"}""")));
        Assert.Equal(["Kzarka"], _timers.Current.Timers.Select(t => t.Name));
    }

    // ---- To-dos

    [Fact]
    public void List_todos_shows_rows_progress_and_the_reset_and_leaves_deleted_lists_out()
    {
        AddList("Dailies", rows: [new TodoRow { Text = "Brew" }, new TodoRow { Text = "Group", Children = [new TodoRow { Text = "A", Done = true }, new TodoRow { Text = "B" }] }]);
        _todos.Update(d => d with { Lists = [.. d.Lists, new TodoList { Name = "Gone", Deleted = true }] });

        var lists = Call("list_todos")["lists"]!.AsArray();

        var list = Assert.Single(lists)!;
        Assert.Equal("1/3", list["progress"]!.GetValue<string>());
        Assert.Equal(360, list["resets"]!["inMinutes"]!.GetValue<int>());
        Assert.Equal(["Brew", "Group"], Names(list["rows"]!, "text"));
        Assert.False(list["rows"]![1]!["done"]!.GetValue<bool>());
        Assert.True(list["rows"]![1]!["children"]![0]!["done"]!.GetValue<bool>());
        Assert.Contains("No to-do list matches", Message(() => Call("list_todos", """{"list":"Gone"}""")));
    }

    [Fact]
    public void Checking_a_row_and_a_group()
    {
        AddList("Dailies", rows: [new TodoRow { Text = "Brew" }, new TodoRow { Text = "Group", Children = [new TodoRow { Text = "A" }, new TodoRow { Text = "B" }] }]);

        var brew = Call("set_todo_done", """{"list":"dailies","row":"brew","done":true}""");
        Assert.True(brew["row"]!["done"]!.GetValue<bool>());
        Assert.Equal("1/3", brew["progress"]!.GetValue<string>());

        var group = Call("set_todo_done", """{"list":"Dailies","row":"Group","done":true}""");
        Assert.Equal("3/3", group["progress"]!.GetValue<string>());
        Assert.True(group["row"]!["done"]!.GetValue<bool>());

        Call("set_todo_done", """{"list":"Dailies","row":"A","done":false}""");
        Assert.Equal("2/3", Call("list_todos")["lists"]![0]!["progress"]!.GetValue<string>());
        Assert.Equal("2/3", _todos.Current.Lists.Select(l => $"{TodoOps.Progress(l.Rows).Done}/{TodoOps.Progress(l.Rows).Total}").Single());
    }

    [Fact]
    public void A_turned_off_list_cannot_be_changed()
    {
        AddList("Weeklies", enabled: false, rows: [new TodoRow { Text = "Brew" }]);

        Assert.Contains("turned off", Message(() => Call("set_todo_done", """{"list":"Weeklies","row":"Brew","done":true}""")));
        Assert.Contains("turned off", Message(() => Call("add_todo", """{"list":"Weeklies","text":"More"}""")));
    }

    [Fact]
    public void Add_todo_appends_or_goes_under_a_row()
    {
        AddList("Dailies", rows: [new TodoRow { Text = "Group", Children = [new TodoRow { Text = "A" }] }, new TodoRow { Text = "Brew", Done = true }]);

        Call("add_todo", """{"list":"Dailies","text":"Last"}""");
        var child = Call("add_todo", """{"list":"Dailies","text":"B","parent":"Group"}""");

        Assert.Equal("B", child["row"]!["text"]!.GetValue<string>());
        var rows = _todos.Current.Lists.Single().Rows;
        Assert.Equal(["Group", "Brew", "Last"], rows.Select(r => r.Text));
        Assert.Equal(["A", "B"], rows[0].Children.Select(c => c.Text));
        Assert.Contains("No row matches", Message(() => Call("add_todo", """{"list":"Dailies","text":"X","parent":"A"}""")));
        Assert.Contains("at most 200", Message(() => Call("add_todo", $$"""{"list":"Dailies","text":"{{new string('x', 201)}}"}""")));
    }

    [Fact]
    public void Adding_a_sub_row_to_a_done_row_makes_the_group_open()
    {
        AddList("Dailies", rows: [new TodoRow { Text = "Brew", Done = true }]);

        Call("add_todo", """{"list":"Dailies","text":"Sub","parent":"Brew"}""");

        Assert.Equal(TodoCheck.Open, TodoOps.Check(_todos.Current.Lists.Single().Rows.Single()));
    }

    // ---- Alerts

    [Fact]
    public void Alerts_can_be_paused_for_a_while_or_until_resumed()
    {
        var timed = Call("pause_alerts", """{"minutes":30}""")["alerts"]!;
        Assert.True(timed["paused"]!.GetValue<bool>());
        Assert.Equal(30, timed["resumesInMinutes"]!.GetValue<int>());
        Assert.Equal(T0.AddMinutes(30), _settings.Current.AlertsPausedUntilUtc);

        var open = Call("pause_alerts")["alerts"]!;
        Assert.True(open["untilResumed"]!.GetValue<bool>());

        Assert.False(Call("resume_alerts")["alerts"]!["paused"]!.GetValue<bool>());
        Assert.Null(_settings.Current.AlertsPausedUntilUtc);
        Assert.Contains("minutes must be from 1 to 1440", Message(() => Call("pause_alerts", """{"minutes":2000}""")));
    }
}
