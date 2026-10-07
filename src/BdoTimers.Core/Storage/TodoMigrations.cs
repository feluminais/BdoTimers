using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.Core.Storage;

public static class TodoMigrations
{
    public static TodoData Apply(TodoData data)
    {
        if (data.DefaultsVersion >= TodoData.CurrentDefaultsVersion) return data;

        var lists = data.Lists.Select(list =>
        {
            if (!list.IsBuiltIn) return list;
            if (list.Id == TodoSeed.DailyId && data.DefaultsVersion < 1) return DailyV1(list);
            if (list.Id == TodoSeed.WeeklyId && data.DefaultsVersion < 2) return WeeklyV2(list);
            return list;
        }).ToList();

        return data with { DefaultsVersion = TodoData.CurrentDefaultsVersion, Lists = lists };
    }

    static TodoList DailyV1(TodoList list) => list with
    {
        Rows = list.Rows
            .Where(row => row.Text != "Claim login and Challenge (Y) rewards")
            .Select(row => row.Text == "Imperial Cooking or Alchemy delivery" ? row with { Text = "Imperial Delivery" } : row)
            .ToList(),
    };

    /// <summary>Garmoth's weekly kills have their own tracker, in Settings, so the row that counted them goes.</summary>
    static TodoList WeeklyV2(TodoList list) => list with { Rows = list.Rows.Where(row => row.Text != "Boss's Roar — Garmoth").ToList() };
}
