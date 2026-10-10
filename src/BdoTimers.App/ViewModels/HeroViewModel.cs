using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>Today's hero: the next boss spawn large, with the one after it, the one before it and how soon it is.</summary>
public sealed partial class HeroViewModel(AppServices services, Action<Guid> openBoss, Action openFollowing) : ObservableObject
{
    SpawnGroup? _group;

    /// <summary>The next spawn's names, pictures, label and clock.</summary>
    public StripTileViewModel Next { get; } = new("Next", elapsed: false);
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ToggleSkipCommand))] private bool _hasNext;
    [ObservableProperty] private UrgencyLevel _level;
    /// <summary>"Previous Golden Pig King · Kutum −08:00:02".</summary>
    [ObservableProperty] private string? _previous;
    [ObservableProperty] private string _skipLabel = "Skip";
    /// <summary>"Muraka · Quint", for the top-bar chip.</summary>
    [ObservableProperty] private string? _namesText;

    public void Update(BossBoardState board, DateTimeOffset now)
    {
        _group = board.Next;
        HasNext = board.Next is not null;
        Next.Update(board.Next, now, services.Art, openBoss);
        SkipLabel = board.Next is { Skipped: true } ? "Unskip" : "Skip";
        NamesText = board.Next is { } shown ? Names(shown) : null;
        Level = board.Next is { } next ? Urgency.Of(next.AtUtc - now) : UrgencyLevel.Normal;
        Previous = board.Previous is { } previous ? $"Previous {Names(previous)} −{DurationFormat.Clock(now - previous.AtUtc)}" : null;
    }

    /// <summary>Skips every boss of the next spawn at once, or brings them all back.</summary>
    [RelayCommand(CanExecute = nameof(HasNext))]
    void ToggleSkip()
    {
        if (_group is { } group) services.Timers.SetMuted(group.Bosses.Select(b => (b.Id, group.AtUtc)), !group.Skipped);
    }

    [RelayCommand]
    void OpenFollowing() => openFollowing();

    static string Names(SpawnGroup group) => string.Join(" · ", group.Bosses.Select(b => b.Name));
}
