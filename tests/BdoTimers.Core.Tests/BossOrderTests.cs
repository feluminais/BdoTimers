using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class BossOrderTests
{
    [Fact]
    public void Both_groups_are_alphabetical_and_custom_names_get_no_priority()
    {
        TimerDef Boss(string name) => new() { Name = name, IsBuiltIn = true };
        var sorted = BossOrder.Sort([Boss("Uturi"), Boss("Karanda"), Boss("Sangoon"), Boss("Bulgasal"),
            Boss("Golden Pig King"), Boss("Garmoth"), new TimerDef { Name = "Bulgasal" }]).ToList();

        Assert.Equal(["Bulgasal", "Golden Pig King", "Sangoon", "Uturi", "Bulgasal", "Garmoth", "Karanda"],
            sorted.Select(t => t.Name));
        Assert.False(sorted[4].IsBuiltIn);
    }
}
