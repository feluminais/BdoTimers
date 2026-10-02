using System.Diagnostics;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class UpdatePanelViewModel(ReleaseInfo release, IPanelHost host) : ObservableObject
{
    public string Version => $"BDO Timers {release.Number}";
    public string ReleasePage => release.Page.AbsoluteUri;
    [ObservableProperty] private string? _error;

    [RelayCommand]
    void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ReleasePage) { UseShellExecute = true });
            host.ClosePanel();
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't open the release page", ex);
            Error = "Couldn't open GitHub";
        }
    }
}
