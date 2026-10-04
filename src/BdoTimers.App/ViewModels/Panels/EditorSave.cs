using BdoTimers.Core.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class EditorSave : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public async Task<bool> RunAsync(Func<Task> save)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Error = null;
        try { await save(); return true; }
        catch (Exception ex)
        {
            Log.Error("Couldn't save editor changes", ex);
            Error = "Couldn't save changes. Try again.";
            return false;
        }
        finally { IsBusy = false; }
    }
}
