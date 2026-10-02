using BdoTimers.App.Alerts;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App.Tests;

public sealed class AlertTestPolicyTests
{
    [Fact]
    public void Explicit_test_can_play_an_unsaved_timer_while_normal_alerts_reject_it()
    {
        var alert = new AlertEvent([new TimerDef { Name = "Test boss" }], DateTimeOffset.UtcNow, 5, 5);
        var state = new AppData();
        Assert.Null(AlertDispatcher.Eligible(state, alert, test: false));
        Assert.Same(alert, AlertDispatcher.Eligible(state, alert, test: true));
    }
}
