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
            if (!list.IsBuiltIn || list.Id != TodoSeed.DailyId) return list;

            var rows = list.Rows
                .Where(row => row.Text != "Claim login and Challenge (Y) rewards")
                .Select(row => row.Text == "Imperial Cooking or Alchemy delivery"
                    ? row with { Text = "Imperial Delivery" }
                    : row)
                .ToList();
            return list with { Rows = rows };
        }).ToList();

        return data with { DefaultsVersion = TodoData.CurrentDefaultsVersion, Lists = lists };
    }
}
