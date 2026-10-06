using System.Globalization;
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
    /// <summary>"Then Garmoth 03:59:58 · Alerts 15 · 5 · at spawn".</summary>
    [ObservableProperty] private string? _then;
    /// <summary>"Previous Golden Pig King · Kutum −08:00:02".</summary>
    [ObservableProperty] private string? _previous;
    [ObservableProperty] private string _skipLabel = "Skip";
    /// <summary>"Muraka · Quint", for the top-bar chip.</summary>
    [ObservableProperty] private string? _namesText;

    public void Update(BossBoardState board, DateTimeOffset now, IReadOnlyList<int> defaultLeads)
    {
        _group = board.Next;
        HasNext = board.Next is not null;
        Next.Update(board.Next, now, services.Art, openBoss);
        SkipLabel = board.Next is { Skipped: true } ? "Unskip" : "Skip";
        NamesText = board.Next is { } shown ? Names(shown) : null;
        Level = board.Next is { } next ? Urgency.Of(next.AtUtc - now) : UrgencyLevel.Normal;
        var parts = new List<string>();
        if (board.FollowedBy is { } then) parts.Add($"Then {Names(then)} {DurationFormat.Clock(then.AtUtc - now)}");
        if (board.Next is { } coming && Leads(coming, defaultLeads) is { Length: > 0 } leads) parts.Add("Alerts " + leads);
        Then = parts.Count == 0 ? null : string.Join(" · ", parts);
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

    /// <summary>The lead times any boss of the group alerts at, earliest first, 0 as "at spawn".</summary>
    static string Leads(SpawnGroup group, IReadOnlyList<int> defaults) => string.Join(" · ", group.Bosses
        .SelectMany(b => b.Alerts.LeadTimes(defaults)).Distinct().OrderByDescending(minutes => minutes)
        .Select(minutes => minutes == 0 ? "at spawn" : minutes.ToString(CultureInfo.InvariantCulture)));
}
