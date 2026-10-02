using BdoTimers.Core.Model;

namespace BdoTimers.Core.Scheduling;

public static class CustomCountdowns
{
    public static bool Includes(TimerDef timer) =>
        timer is { IsBuiltIn: false, Preset: null, Kind: TimerKind.Countdown, Countdown: not null };

    public static string ActionName(CountdownStatus status) => status switch
    {
        CountdownStatus.Running => "Pause",
        CountdownStatus.Paused => "Resume",
        _ => "Start",
    };
}
