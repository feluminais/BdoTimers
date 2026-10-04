using BdoTimers.App.ViewModels.Panels;
using System.IO;

namespace BdoTimers.App.Tests;

public class EditorSaveTests
{
    [Fact]
    public async Task SavingWaitsForGenerationAndPreventsDuplicateSaves()
    {
        var generated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = false;
        var state = new EditorSave();
        var task = state.RunAsync(async () => { await generated.Task; saved = true; });
        Assert.True(state.IsBusy);
        Assert.False(saved);
        Assert.False(await state.RunAsync(() => throw new Exception("Duplicate")));
        generated.SetResult();
        Assert.True(await task);
        Assert.True(saved);
        Assert.False(state.IsBusy);
    }

    [Fact]
    public async Task GenerationFailureKeepsEditorOpenAndAllowsRetry()
    {
        var state = new EditorSave();
        Assert.False(await state.RunAsync(() => throw new IOException("Disk full")));
        Assert.NotNull(state.Error);
        Assert.False(state.IsBusy);
        Assert.True(await state.RunAsync(() => Task.CompletedTask));
        Assert.Null(state.Error);
    }
}
