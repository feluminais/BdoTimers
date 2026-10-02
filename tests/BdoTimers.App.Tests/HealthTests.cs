using System.IO;
using System.Windows.Threading;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Tests;

public sealed class HealthTests
{
    [Fact]
    public void Failed_voice_is_visible_until_real_recovery_and_repeated_notices_are_throttled() => WpfTest.Run(() =>
    {
        var health = new AppHealth(new FixedClock(), Dispatcher.CurrentDispatcher);
        var changes = 0;
        health.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppHealth.Status)) changes++; };
        health.Failed("Voice", new FileNotFoundException());
        Assert.Contains("Repair the installation", health.Status);
        health.Failed("Voice", new FileNotFoundException());
        Assert.Equal(1, changes);
        Assert.Equal(2, health.LastFailure!.Count);
        health.Failed("Sound", new IOException());
        health.Succeeded("Voice");
        Assert.DoesNotContain("Voice files", health.Status);
        Assert.Contains("output device", health.Status);
        health.Succeeded("Sound");
        Assert.Null(health.Status);
        Assert.NotNull(health.LastFailure);
    });

    [Fact]
    public void Background_channel_failure_is_marshaled_to_the_ui_dispatcher() => WpfTest.Run(() =>
    {
        var health = new AppHealth(new FixedClock(), Dispatcher.CurrentDispatcher);
        var notifications = new List<int>();
        health.PropertyChanged += (_, _) => notifications.Add(Environment.CurrentManagedThreadId);
        var uiThread = Environment.CurrentManagedThreadId;
        Task.Run(() => health.Failed("Notifications", new IOException())).GetAwaiter().GetResult();
        WpfTest.Drain();
        Assert.Contains("notification failed", health.Status);
        Assert.All(notifications, id => Assert.Equal(uiThread, id));
        Assert.NotEmpty(notifications);
    });

    sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void Unrelated_store_success_cannot_hide_a_failed_settings_save() => WpfTest.Run(() =>
    {
        var health = new AppHealth(new FixedClock(), Dispatcher.CurrentDispatcher);
        health.Failed("Saving", new StateSaveException("settings.json", new IOException("locked")));
        health.Saved("timers.json");
        Assert.Contains("Settings couldn't be saved", health.Status);
        health.Saved("settings.json");
        Assert.Null(health.Status);
    });
}
