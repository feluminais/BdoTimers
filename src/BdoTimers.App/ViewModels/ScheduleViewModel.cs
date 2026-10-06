using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>The Schedule screen: this week's boss grid and the month, with the Following list one click away.</summary>
public sealed partial class ScheduleViewModel(BossesViewModel week, CalendarViewModel month, Action openFollowing)
{
    public BossesViewModel Week => week;
    public CalendarViewModel Month => month;

    [RelayCommand]
    void OpenFollowing() => openFollowing();
}
