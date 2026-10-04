using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.Core.Tests;

public class EditDraftTests
{
    [Fact]
    public void EditsDoNotChangeLiveDataAndCommitPreservesUnrelatedRuntimeChanges()
    {
        var original = new TimerDef { Name = "Old", Countdown = new() };
        var draft = new EditDraft<TimerDef>(original);
        draft.Update(t => t with { Name = "New" });
        var running = original with { Countdown = original.Countdown! with { Status = CountdownStatus.Running } };
        var saved = draft.Apply(running);
        Assert.Equal("Old", original.Name);
        Assert.Equal("New", saved.Name);
        Assert.Equal(CountdownStatus.Running, saved.Countdown!.Status);
        Assert.True(draft.HasChanges);
    }

    [Fact]
    public void RevertingAnEditDoesNotRequireSaving()
    {
        var original = new AppSettings();
        var draft = new EditDraft<AppSettings>(original);
        draft.Update(s => s with { TtsRate = 2 });
        draft.Update(s => s with { TtsRate = original.TtsRate });
        Assert.False(draft.HasChanges);
        Assert.Same(original, draft.Apply(original));
    }

    [Fact]
    public void RepeatedDurationEditsApplyOnlyFinalDurationToLatestPausedState()
    {
        var original = new TimerDef { Name = "Old", Countdown = new() { Duration = TimeSpan.FromMinutes(60), Status = CountdownStatus.Paused, Remaining = TimeSpan.FromMinutes(10) } };
        var draft = new EditDraft<TimerDef>(original);
        draft.Update(t => t with { Countdown = BdoTimers.Core.Scheduling.CountdownOps.ChangeDuration(t.Countdown!, TimeSpan.FromMinutes(1)) }, "duration");
        draft.Update(t => t with { Countdown = BdoTimers.Core.Scheduling.CountdownOps.ChangeDuration(t.Countdown!, TimeSpan.FromMinutes(60)) }, "duration");
        draft.Update(t => t with { Name = "New" });
        Assert.Equal(TimeSpan.FromMinutes(10), draft.Apply(original).Countdown!.Remaining);
    }
}
