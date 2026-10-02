using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Seed;

public static class TodoSeed
{
    public static readonly Guid WeeklyId = Guid.Parse("1f78d70a-88be-4568-8723-dd55452809ae");
    public static readonly Guid DailyId = Guid.Parse("85d0bd11-9962-4f2d-b607-71c098bd7a52");

    public static TodoData Create(DateTimeOffset nowUtc, AppSettings settings) => new()
    {
        DefaultsVersion = TodoData.CurrentDefaultsVersion,
        Lists =
        [
            new TodoList
            {
                Id = WeeklyId, Name = "Weekly quests", Cadence = TodoCadence.Weekly, IsBuiltIn = true,
                NextResetUtc = TodoReset.Next(TodoCadence.Weekly, settings.WeeklyTodoReset, nowUtc),
                Rows =
                [
                    Row("Throne of Edana — weekly boss quest"),
                    Row("Boss's Roar — Garmoth", Row("1"), Row("2"), Row("3")),
                    Row("Olvia Academy", Row("Bulletin board quest 1"), Row("Bulletin board quest 2"),
                        Row("Bulletin board quest 3"), Row("Cliff — Operation Bumblin' Buccaneers Brawl")),
                    Row("Liana's weekly life skill quests", Row("Gathering — Fairy Powder"),
                        Row("Hunting — Fire Horn"), Row("Cooking — Witch's Delicacy"),
                        Row("Alchemy — Mysterious Catalyst"), Row("Fishing — Relic Crystal Shard or Silver Key"),
                        Row("Farming — Mysterious Seed or Moles"), Row("Training — Horse Taming"),
                        Row("Crossroad — Carrot Confit or Krogdalo's Origin Stone")),
                ],
            },
            new TodoList
            {
                Id = DailyId, Name = "Daily tasks", Cadence = TodoCadence.Daily, IsBuiltIn = true,
                NextResetUtc = TodoReset.Next(TodoCadence.Daily, settings.DailyTodoReset, nowUtc),
                Rows =
                [
                    Row("Liana / Ludowig daily life skill quest"),
                    Row("Imperial Delivery"),
                    Row("Pit of the Undying"),
                ],
            },
        ],
    };

    static TodoRow Row(string text, params TodoRow[] children) => new() { Text = text, Children = children };
}
