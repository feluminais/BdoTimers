using System.Windows.Threading;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App;

/// <summary>One UI-thread tick per second shared by all view models and the overlay.</summary>
public sealed class UiClock
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public event Action<DateTimeOffset>? Tick;

    public UiClock(IClock clock) => _timer.Tick += (_, _) => Tick?.Invoke(clock.UtcNow);

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
}
