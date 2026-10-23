using System.Text.Json.Nodes;

namespace BdoTimers.Core.Agent;

/// <summary>A tool as an MCP client lists it. A destructive tool removes something.</summary>
public sealed record AgentToolInfo(string Name, string Description, JsonObject InputSchema, bool ReadOnly, bool Destructive = false);

/// <summary>The tools <see cref="AgentTools"/> runs. The bridge lists them without the app running, so they live here.</summary>
public static class AgentCatalog
{
    public static IReadOnlyList<AgentToolInfo> Tools { get; } =
    [
        Read("get_status",
            "Now in UTC and local time, the in-game day or night clock, whether alerts are paused, and the countdowns and stopwatches that are running.",
            Schema([])),
        Read("list_timers",
            "Lists timers: countdowns, stopwatches, weekly timers and one-time events with their state and next time. World bosses are left out unless asked for.",
            Schema([],
                ("kind", Enum("Only this kind.", "scheduled", "countdown", "stopwatch", "event")),
                ("query", Text("Only timers whose name contains this.")),
                ("bosses", Bool("Include world bosses of the selected region. Default false.")))),
        Read("get_upcoming",
            "The next times timers fire, soonest first: boss spawns, weekly timers, events and running countdowns. Skipped spawns are left out.",
            Schema([],
                ("hours", Int("How far ahead to look, 1 to 168. Default 24.")),
                ("limit", Int("At most this many results, 1 to 50. Default 15.")),
                ("bosses", Bool("Include world bosses. Default true.")))),
        Read("list_todos",
            "The to-do lists with their rows, what is done, and when each resets.",
            Schema([], ("list", Text("Only this list, by id or name.")))),
        Write("control_timer",
            "Starts, pauses, resumes or resets a countdown or stopwatch.",
            Schema(["timer", "action"],
                ("timer", Text("The timer's id or name.")),
                ("action", Enum("What to do.", "start", "pause", "resume", "reset")))),
        Write("create_countdown",
            "Adds a countdown timer.",
            Schema(["name", "minutes"],
                ("name", Text("The countdown's name.")),
                ("minutes", Int("How long it runs, 1 to 1440.")),
                ("start", Bool("Start it now. Default false.")))),
        Write("create_event",
            "Adds a one-time event that alerts at a date and time.",
            Schema(["name", "at"],
                ("name", Text("The event's name.")),
                ("at", Text("Date and time as yyyy-MM-dd HH:mm, in the time zone below.")),
                ("timeZone", Text("A time zone id. Default is this PC's.")))),
        Write("set_timer_enabled",
            "Turns a timer, boss included, on or off. Off timers don't alert.",
            Schema(["timer", "enabled"],
                ("timer", Text("The timer's id or name.")),
                ("enabled", Bool("True to turn it on.")))),
        Write("delete_timer",
            "Deletes a timer you made. Built-in timers and presets can't be deleted. The app offers to undo it.",
            Schema(["timer"], ("timer", Text("The timer's id or name."))),
            destructive: true),
        Write("set_todo_done",
            "Checks or unchecks a to-do row. A row with sub-rows sets them all.",
            Schema(["list", "row", "done"],
                ("list", Text("The list's id or name.")),
                ("row", Text("The row's id or text.")),
                ("done", Bool("True to check it.")))),
        Write("add_todo",
            "Adds a row to the end of a to-do list, or as a sub-row of one of its rows.",
            Schema(["list", "text"],
                ("list", Text("The list's id or name.")),
                ("text", Text("The row's text.")),
                ("parent", Text("A row of the list, by id or text, to add it under.")))),
        Write("pause_alerts",
            "Pauses all alerts for a while, or until resumed.",
            Schema([], ("minutes", Int("For how long, 1 to 1440. Leave out to pause until resumed.")))),
        Write("resume_alerts", "Resumes alerts that were paused.", Schema([])),
    ];

    static AgentToolInfo Read(string name, string description, JsonObject schema) => new(name, description, schema, ReadOnly: true);

    static AgentToolInfo Write(string name, string description, JsonObject schema, bool destructive = false) =>
        new(name, description, schema, ReadOnly: false, Destructive: destructive);

    static JsonObject Schema(string[] required, params (string Name, JsonObject Property)[] properties)
    {
        var props = new JsonObject();
        foreach (var (name, property) in properties) props[name] = property;
        var schema = new JsonObject { ["type"] = "object", ["properties"] = props };
        if (required.Length > 0) schema["required"] = Strings(required);
        return schema;
    }

    static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };
    static JsonObject Int(string description) => new() { ["type"] = "integer", ["description"] = description };
    static JsonObject Bool(string description) => new() { ["type"] = "boolean", ["description"] = description };

    static JsonObject Enum(string description, params string[] values) =>
        new() { ["type"] = "string", ["description"] = description, ["enum"] = Strings(values) };

    static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());
}
