using System.Globalization;
using System.Text.Json.Nodes;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Agent;

/// <summary>What the app does itself where the stores alone would skip it, such as keeping a deletion undoable.</summary>
public sealed record AgentHooks(Func<Guid, bool>? DeleteTimer = null, Func<Guid, bool>? ResetTimer = null,
    Func<Guid, bool>? DeleteBoss = null);

/// <summary>
/// The tools an agent can call (see <see cref="AgentCatalog"/>). They change state through the same stores as the UI, and
/// read the state the stores hold. Not thread-affine, but the app calls them on the UI thread because the stores'
/// <c>Changed</c> handlers touch UI collections.
/// </summary>
public sealed class AgentTools(TimerStore timers, TodoStore todos, PersistentState<AppSettings> settings, IClock clock,
    TimeZoneInfo zone, AgentHooks? hooks = null)
{
    const int MaxNameLength = 80;
    const int MaxTodoLength = 200;

    public JsonNode Call(string name, JsonObject args) => name switch
    {
        "get_status" => GetStatus(),
        "list_timers" => ListTimers(args),
        "get_upcoming" => GetUpcoming(args),
        "list_todos" => ListTodos(args),
        "control_timer" => ControlTimer(args),
        "create_countdown" => CreateCountdown(args),
        "create_event" => CreateEvent(args),
        "add_boss" => AddBoss(args),
        "set_timer_enabled" => SetTimerEnabled(args),
        "delete_timer" => DeleteTimer(args),
        "set_todo_done" => SetTodoDone(args),
        "add_todo" => AddTodo(args),
        "pause_alerts" => PauseAlerts(args),
        "resume_alerts" => ResumeAlerts(),
        _ => throw new AgentToolException($"Unknown tool '{name}'."),
    };

    // ---- Reading

    JsonNode GetStatus()
    {
        var now = clock.UtcNow;
        var data = timers.Current;
        var silenced = GarmothTracker.Silenced(data);
        var game = GameClock.At(now);
        var running = data.Timers
            .Where(t => BossRegions.IsEligible(data, t) && Status(t) == CountdownStatus.Running && (t.Countdown is not null || t.Stopwatch is not null))
            .Select(t => Summary(t, now, silenced));
        return new JsonObject
        {
            ["nowUtc"] = Utc(now),
            ["nowLocal"] = Local(now),
            ["timeZone"] = zone.Id,
            ["bossRegion"] = data.SelectedBossRegion,
            ["game"] = new JsonObject
            {
                ["time"] = game.Time.ToString("HH:mm", CultureInfo.InvariantCulture),
                ["isNight"] = game.IsNight,
                [game.IsNight ? "dayBreaksInMinutes" : "nightFallsInMinutes"] = Minutes(game.Left),
            },
            ["alerts"] = AlertsStatus(now),
            ["running"] = new JsonArray(running.ToArray()),
        };
    }

    JsonNode ListTimers(JsonObject args)
    {
        var now = clock.UtcNow;
        var data = timers.Current;
        var silenced = GarmothTracker.Silenced(data);
        var kind = Optional(args, "kind")?.ToLowerInvariant();
        if (kind is not null && kind is not ("scheduled" or "countdown" or "stopwatch" or "event"))
            throw new AgentToolException("kind must be scheduled, countdown, stopwatch or event.");
        var query = Optional(args, "query");
        var bosses = OptionalBool(args, "bosses") ?? false;
        var found = data.Timers
            .Where(t => BossRegions.IsEligible(data, t) && (bosses || !t.IsBuiltIn))
            .Where(t => kind is null || KindName(t.Kind) == kind)
            .Where(t => query is null || t.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(t => Summary(t, now, silenced))
            .ToArray();
        return new JsonObject { ["count"] = found.Length, ["timers"] = new JsonArray(found) };
    }

    JsonNode GetUpcoming(JsonObject args)
    {
        var now = clock.UtcNow;
        var data = timers.Current;
        var hours = OptionalInt(args, "hours", 1, 168) ?? 24;
        var limit = OptionalInt(args, "limit", 1, 50) ?? 15;
        var bosses = OptionalBool(args, "bosses") ?? true;
        var silenced = GarmothTracker.Silenced(data);
        var until = now + TimeSpan.FromHours(hours);
        var items = data.Timers
            .Where(t => t.Enabled && BossRegions.IsEligible(data, t) && (bosses || !t.IsBuiltIn))
            .SelectMany(t => OccurrenceSource.Between(t, now, until)
                .Where(at => !silenced.Contains(new MutedOccurrence(t.Id, at)))
                .Take(limit)
                .Select(at => (Timer: t, At: at)))
            .OrderBy(i => i.At)
            .ThenBy(i => BossOrder.Priority(i.Timer))
            .ThenBy(i => i.Timer.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(i =>
            {
                var o = new JsonObject
                {
                    ["id"] = i.Timer.Id.ToString(),
                    ["name"] = OccurrenceSource.NameAt(i.Timer, i.At),
                    ["kind"] = KindName(i.Timer.Kind),
                };
                if (i.Timer.IsBuiltIn) o["boss"] = true;
                return (JsonNode)Merge(o, When(i.At, now));
            })
            .ToArray();
        return new JsonObject { ["nowUtc"] = Utc(now), ["upcoming"] = new JsonArray(items) };
    }

    JsonNode ListTodos(JsonObject args)
    {
        var now = clock.UtcNow;
        var lists = todos.Current.Lists.Where(l => !l.Deleted).ToList();
        if (Optional(args, "list") is { } key) lists = [Pick(lists, key, l => l.Id, l => l.Name, "to-do list")];
        return new JsonObject { ["lists"] = new JsonArray(lists.Select(l => TodoSummary(l, now)).ToArray()) };
    }

    // ---- Timers

    JsonNode ControlTimer(JsonObject args)
    {
        var now = clock.UtcNow;
        var timer = FindTimer(Required(args, "timer"));
        var action = Required(args, "action").ToLowerInvariant();
        if (timer.Preset is Presets.HorseRegistration or Presets.HorseRegistrationRun || (timer.Countdown is null && timer.Stopwatch is null))
            throw new AgentToolException($"'{timer.Name}' isn't a countdown or stopwatch that can be controlled here.");
        var status = Status(timer);
        switch (action)
        {
            case "start":
                Require(status == CountdownStatus.Idle, $"'{timer.Name}' is {Word(status)}; " + (status == CountdownStatus.Paused ? "resume it, or reset it to start over." : "reset it to start over."));
                timers.Start(timer.Id, now);
                break;
            case "pause":
                Require(status == CountdownStatus.Running, $"'{timer.Name}' isn't running.");
                timers.Pause(timer.Id, now);
                break;
            case "resume":
                Require(status == CountdownStatus.Paused, $"'{timer.Name}' isn't paused.");
                timers.Resume(timer.Id, now);
                break;
            case "reset":
                if (hooks?.ResetTimer is { } reset) reset(timer.Id);
                else timers.Reset(timer.Id);
                break;
            default:
                throw new AgentToolException("action must be start, pause, resume or reset.");
        }
        return Fresh(timer.Id, now);
    }

    JsonNode CreateCountdown(JsonObject args)
    {
        var now = clock.UtcNow;
        var name = Name(Required(args, "name"), MaxNameLength);
        var minutes = RequiredInt(args, "minutes", 1, 24 * 60);
        var countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(minutes) };
        if (OptionalBool(args, "start") == true) countdown = CountdownOps.Start(countdown, now);
        var timer = new TimerDef { Name = name, Kind = TimerKind.Countdown, Countdown = countdown };
        timers.Upsert(timer);
        return Fresh(timer.Id, now);
    }

    JsonNode CreateEvent(JsonObject args)
    {
        var now = clock.UtcNow;
        var name = Name(Required(args, "name"), MaxNameLength);
        var zoneId = Optional(args, "timeZone") ?? zone.Id;
        try { TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new AgentToolException($"Unknown time zone '{zoneId}'.");
        }
        var text = Required(args, "at");
        if (!DateTime.TryParseExact(text, ["yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            throw new AgentToolException("at must look like 2026-10-24 20:30.");
        var spec = new OneTimeSpec { Date = DateOnly.FromDateTime(at), Time = TimeOnly.FromDateTime(at), TimeZoneId = zoneId };
        if (OneTimeEvents.AtUtc(spec) <= now) throw new AgentToolException("That time has already passed.");
        var timer = new TimerDef { Name = name, Kind = TimerKind.OneTime, OneTime = spec };
        timers.Upsert(timer);
        return Fresh(timer.Id, now);
    }

    JsonNode AddBoss(JsonObject args)
    {
        var data = timers.Current;
        var region = BossRegions.Find(data.SelectedBossRegion);
        var name = Name(Required(args, "name"), MaxNameLength);
        if (!BossEdits.IsNameFree(data, region.Id, name))
            throw new AgentToolException($"Another {region.ShortLabel} boss is already named '{name}'.");
        var times = RequiredList(args, "times").Select(ParseTime).Distinct().Order().ToList();
        var days = OptionalList(args, "days")?.Select(ParseDay).Distinct().ToList() ?? [.. Enum.GetValues<DayOfWeek>()];
        var start = OptionalDate(args, "from");
        var end = OptionalDate(args, "to");
        try { ScheduleMath.ValidateDateRange(start, end); }
        catch (ArgumentException ex) { throw new AgentToolException(ex.Message); }
        var slots = days.OrderBy(d => ((int)d + 6) % 7).SelectMany(d => times.Select(t => new Slot(d, t))).ToList();
        var boss = BossEdits.Create(data, name, slots, start, end);
        timers.Upsert(boss);
        return Fresh(boss.Id, clock.UtcNow);
    }

    JsonNode SetTimerEnabled(JsonObject args)
    {
        var timer = FindTimer(Required(args, "timer"));
        timers.SetEnabled(timer.Id, RequiredBool(args, "enabled"));
        return Fresh(timer.Id, clock.UtcNow);
    }

    JsonNode DeleteTimer(JsonObject args)
    {
        var timer = FindTimer(Required(args, "timer"));
        if (timer.AddedByUser)
        {
            if (hooks?.DeleteBoss is { } deleteBoss) deleteBoss(timer.Id);
            else timers.Delete(timer.Id);
        }
        else if (timer.IsBuiltIn || timer.Preset is not null)
            throw new AgentToolException($"'{timer.Name}' is built in and can't be deleted. Turn it off instead.");
        else if (hooks?.DeleteTimer is { } delete) delete(timer.Id);
        else timers.Delete(timer.Id);
        return new JsonObject { ["deleted"] = new JsonObject { ["id"] = timer.Id.ToString(), ["name"] = timer.Name } };
    }

    // ---- To-dos

    JsonNode SetTodoDone(JsonObject args)
    {
        var list = FindList(Required(args, "list"));
        var done = RequiredBool(args, "done");
        var row = Pick(AllRows(list.Rows).ToList(), Required(args, "row"), r => r.Id, r => r.Text, "row");
        todos.Modify(list.Id, l => l with { Rows = l.Rows.Select(r => SetIn(r, row.Id, done)).ToList() });
        var after = todos.Current.Lists.First(l => l.Id == list.Id);
        return new JsonObject
        {
            ["row"] = RowJson(AllRows(after.Rows).First(r => r.Id == row.Id)),
            ["progress"] = Progress(after),
        };
    }

    JsonNode AddTodo(JsonObject args)
    {
        var list = FindList(Required(args, "list"));
        var text = Name(Required(args, "text"), MaxTodoLength);
        var outline = TodoOutline.Flatten(list.Rows);
        var row = new TodoOutlineRow(Guid.NewGuid(), text, false, 0);
        if (Optional(args, "parent") is { } parentKey)
        {
            var parent = Pick(outline.Where(r => r.Level == 0).ToList(), parentKey, r => r.Id, r => r.Text, "row");
            var at = outline.FindIndex(r => r.Id == parent.Id) + 1;
            while (at < outline.Count && outline[at].Level == 1) at++;
            outline.Insert(at, row with { Level = 1 });
        }
        else outline.Add(row);
        todos.ReplaceRows(list.Id, TodoOutline.Build(outline));
        var after = todos.Current.Lists.First(l => l.Id == list.Id);
        return new JsonObject
        {
            ["row"] = RowJson(AllRows(after.Rows).First(r => r.Id == row.Id)),
            ["progress"] = Progress(after),
        };
    }

    // ---- Alerts

    JsonNode PauseAlerts(JsonObject args)
    {
        var now = clock.UtcNow;
        var minutes = OptionalInt(args, "minutes", 1, 24 * 60);
        settings.Update(s => AlertPause.Pause(s, now, minutes is { } m ? TimeSpan.FromMinutes(m) : null));
        return new JsonObject { ["alerts"] = AlertsStatus(now) };
    }

    JsonNode ResumeAlerts()
    {
        settings.Update(AlertPause.Resume);
        return new JsonObject { ["alerts"] = AlertsStatus(clock.UtcNow) };
    }

    JsonObject AlertsStatus(DateTimeOffset now)
    {
        var s = settings.Current;
        var paused = AlertPause.IsPaused(s, now);
        var o = new JsonObject { ["paused"] = paused };
        if (paused && AlertPause.Remaining(s, now) is { } left) o["resumesInMinutes"] = Minutes(left);
        else if (paused) o["untilResumed"] = true;
        return o;
    }

    // ---- JSON

    JsonObject Summary(TimerDef t, DateTimeOffset now, HashSet<MutedOccurrence> silenced)
    {
        var o = new JsonObject
        {
            ["id"] = t.Id.ToString(), ["name"] = t.Name, ["kind"] = KindName(t.Kind), ["enabled"] = t.Enabled,
        };
        if (t.Preset is not null) o["preset"] = t.Preset;
        if (t.IsBuiltIn) o["boss"] = true;
        switch (t)
        {
            case { Countdown: { } c }:
                o["status"] = Word(c.Status);
                o["durationMinutes"] = Minutes(c.Duration);
                if (CountdownOps.Left(c, now) is { } left)
                {
                    o["leftSeconds"] = (long)left.TotalSeconds;
                    o["left"] = left > TimeSpan.Zero ? Span(left) : "ready";
                    o["progressPercent"] = CountdownOps.Progress(c, now, Presets.Overgrows(t.Preset));
                }
                break;
            case { Stopwatch: { } s }:
                o["status"] = Word(s.Status);
                var elapsed = StopwatchOps.Elapsed(s, now);
                o["elapsedSeconds"] = (long)elapsed.TotalSeconds;
                o["elapsed"] = Span(elapsed);
                break;
            case { Scheduled: { } spec }:
                if (spec.Off) o["off"] = true;
                if (!t.IsBuiltIn || t.AddedByUser) o["schedule"] = Schedule(spec);
                if (spec.StartDate is { } from) o["from"] = Date(from);
                if (spec.EndDate is { } to) o["to"] = Date(to);
                break;
            case { OneTime: { } one }:
                if (one.Finished) o["finished"] = true;
                break;
        }
        if (t.Enabled && OccurrenceSource.From(t, now).Where(at => !silenced.Contains(new MutedOccurrence(t.Id, at))).Cast<DateTimeOffset?>().FirstOrDefault() is { } next)
        {
            var when = When(next, now);
            if (OccurrenceSource.NameAt(t, next) is var title && title != t.Name) when["title"] = title;
            o["next"] = when;
        }
        return o;
    }

    JsonObject TodoSummary(TodoList list, DateTimeOffset now) => new()
    {
        ["id"] = list.Id.ToString(),
        ["name"] = list.Name,
        ["cadence"] = list.Cadence == TodoCadence.Daily ? "daily" : "weekly",
        ["enabled"] = list.Enabled,
        ["progress"] = Progress(list),
        ["resets"] = When(list.NextResetUtc, now),
        ["rows"] = new JsonArray(list.Rows.Select(RowJson).ToArray()),
    };

    static JsonNode RowJson(TodoRow row)
    {
        var o = new JsonObject { ["id"] = row.Id.ToString(), ["text"] = row.Text, ["done"] = TodoOps.Check(row) == TodoCheck.Done };
        if (row.Children.Count > 0) o["children"] = new JsonArray(row.Children.Select(RowJson).ToArray());
        return o;
    }

    static string Progress(TodoList list)
    {
        var (done, total) = TodoOps.Progress(list.Rows);
        return $"{done}/{total}";
    }

    JsonObject When(DateTimeOffset at, DateTimeOffset now) => new()
    {
        ["utc"] = Utc(at), ["local"] = Local(at), ["inMinutes"] = (int)Math.Floor((at - now).TotalMinutes),
    };

    static JsonObject Merge(JsonObject target, JsonObject extra)
    {
        foreach (var (key, value) in extra.ToList())
        {
            extra.Remove(key);
            target[key] = value;
        }
        return target;
    }

    JsonNode Fresh(Guid id, DateTimeOffset now)
    {
        var data = timers.Current;
        return Summary(data.Timers.First(t => t.Id == id), now, GarmothTracker.Silenced(data));
    }

    static string Utc(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    string Local(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static int Minutes(TimeSpan span) => (int)Math.Floor(span.TotalMinutes);

    static string Span(TimeSpan span)
    {
        span = span.Duration();
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes:00}m" : $"{span.Minutes}m {span.Seconds:00}s";
    }

    static string Schedule(ScheduledSpec spec) =>
        string.Join(", ", spec.Slots.Select(s => $"{s.Day.ToString()[..3]} {s.Time:HH:mm}" + (s.Label is { Length: > 0 } label ? $" {label}" : "")))
        + $" ({spec.TimeZoneId})";

    static string KindName(TimerKind kind) => kind switch
    {
        TimerKind.Scheduled => "scheduled",
        TimerKind.Countdown => "countdown",
        TimerKind.Stopwatch => "stopwatch",
        _ => "event",
    };

    static string Word(CountdownStatus status) => status switch
    {
        CountdownStatus.Running => "running",
        CountdownStatus.Paused => "paused",
        _ => "idle",
    };

    static CountdownStatus Status(TimerDef t) => t.Countdown?.Status ?? t.Stopwatch?.Status ?? CountdownStatus.Idle;

    // ---- Finding things

    TimerDef FindTimer(string key)
    {
        var data = timers.Current;
        return Pick(data.Timers.Where(t => BossRegions.IsEligible(data, t)).ToList(), key, t => t.Id, t => t.Name, "timer");
    }

    TodoList FindList(string key) =>
        Pick(todos.Current.Lists.Where(l => !l.Deleted).ToList(), key, l => l.Id, l => l.Name, "to-do list") is { Enabled: true } list
            ? list
            : throw new AgentToolException("That list is turned off.");

    static IEnumerable<TodoRow> AllRows(IEnumerable<TodoRow> rows) => rows.SelectMany(r => new[] { r }.Concat(r.Children));

    static TodoRow SetIn(TodoRow row, Guid id, bool done) => row.Id == id ? TodoOps.SetDone(row, done)
        : row with { Children = row.Children.Select(c => SetIn(c, id, done)).ToList() };

    /// <summary>The item with this id, else the one named this in any case. A near miss is never taken for the item, since
    /// the tools that use this change things; the error lists the names that contain the key.</summary>
    static T Pick<T>(IReadOnlyList<T> items, string key, Func<T, Guid> id, Func<T, string> name, string noun)
    {
        key = key.Trim();
        if (Guid.TryParse(key, out var guid) && items.FirstOrDefault(i => id(i) == guid) is { } byId) return byId;
        string Describe(IEnumerable<T> found) => string.Join("; ", found.Take(6).Select(m => $"{name(m)} ({id(m)})"));
        var exact = items.Where(i => string.Equals(name(i), key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1) return exact[0];
        if (exact.Count > 1) throw new AgentToolException($"More than one {noun} is named '{key}': {Describe(exact)}. Use the id.");
        var near = items.Where(i => name(i).Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
        throw new AgentToolException($"No {noun} matches '{key}'." + (near.Count > 0 ? $" Did you mean: {Describe(near)}?" : ""));
    }

    // ---- Arguments

    static string Name(string text, int max)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) throw new AgentToolException("The text can't be empty.");
        if (trimmed.Length > max) throw new AgentToolException($"The text can be at most {max} characters.");
        return trimmed;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new AgentToolException(message);
    }

    static string? Optional(JsonObject args, string key) => args[key] switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => string.IsNullOrWhiteSpace(s) ? null : s,
        null => null,
        _ => throw new AgentToolException($"{key} must be text."),
    };

    static string Required(JsonObject args, string key) =>
        Optional(args, key) ?? throw new AgentToolException($"{key} is required.");

    static bool? OptionalBool(JsonObject args, string key) => args[key] switch
    {
        null => null,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) && bool.TryParse(s, out var b) => b,
        _ => throw new AgentToolException($"{key} must be true or false."),
    };

    static bool RequiredBool(JsonObject args, string key) =>
        OptionalBool(args, key) ?? throw new AgentToolException($"{key} is required.");

    static int? OptionalInt(JsonObject args, string key, int min, int max)
    {
        if (args[key] is null) return null;
        int value;
        if (args[key] is JsonValue v && (v.TryGetValue<int>(out value)
            || (v.TryGetValue<double>(out var d) && d == Math.Floor(d) && Math.Abs(d) < int.MaxValue && (value = (int)d) == d)
            || (v.TryGetValue<string>(out var s) && int.TryParse(s, CultureInfo.InvariantCulture, out value))))
        {
            if (value < min || value > max) throw new AgentToolException($"{key} must be from {min} to {max}.");
            return value;
        }
        throw new AgentToolException($"{key} must be a whole number.");
    }

    static int RequiredInt(JsonObject args, string key, int min, int max) =>
        OptionalInt(args, key, min, max) ?? throw new AgentToolException($"{key} is required.");

    /// <summary>A list of text; a lone text counts as a list of one.</summary>
    static List<string>? OptionalList(JsonObject args, string key)
    {
        if (args[key] is JsonArray array)
        {
            var items = array.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)
                ? s.Trim() : throw new AgentToolException($"{key} must be a list of text.")).ToList();
            return items.Count > 0 ? items : null;
        }
        return Optional(args, key) is { } one ? [one.Trim()] : null;
    }

    static List<string> RequiredList(JsonObject args, string key) =>
        OptionalList(args, key) ?? throw new AgentToolException($"{key} is required.");

    static TimeOnly ParseTime(string text) =>
        TimeOnly.TryParseExact(text, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time : throw new AgentToolException($"'{text}' isn't a time like 19:30.");

    /// <summary>A day by its English name or first three letters, in any case.</summary>
    static DayOfWeek ParseDay(string text) =>
        Enum.GetValues<DayOfWeek>().Cast<DayOfWeek?>().FirstOrDefault(d =>
            string.Equals(d.ToString(), text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.ToString()![..3], text, StringComparison.OrdinalIgnoreCase))
        ?? throw new AgentToolException($"'{text}' isn't a day like Monday.");

    static DateOnly? OptionalDate(JsonObject args, string key) => Optional(args, key) is not { } text ? null
        : DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : throw new AgentToolException($"{key} must look like 2026-10-22.");
}
