using System.Collections.ObjectModel;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>
/// A task of a Today panel; the first of a list shows the list's name when more than one list is on. A task with sub-tasks
/// is one line, with how far along it is, and opens to show them under it.
/// </summary>
public sealed partial class ListedTask : ObservableObject
{
    public ListedTask(string? listName, TodoRowViewModel row)
    {
        _listName = listName;
        Row = row;
        row.Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasChildren));
    }

    public TodoRowViewModel Row { get; }
    [ObservableProperty] private string? _listName;
    [ObservableProperty] private bool _isExpanded;
    public bool HasChildren => Row.Children.Count > 0;

    [RelayCommand]
    void ToggleExpanded() => IsExpanded = !IsExpanded;
}

/// <summary>
/// One of Today's task panels, Daily or Weekly: what is still open in the active lists of its cadence, how far along they
/// are, and a way on to the To-do screen.
/// </summary>
public sealed partial class TaskPanelViewModel : ObservableObject
{
    /// <summary>Open tasks a panel has room for; the rest are counted.</summary>
    const int MaxTasks = 6;

    readonly AppServices _services;
    readonly TodoCadence _cadence;
    readonly TodoViewModel _todo;
    readonly Action _showTodo;

    public ObservableCollection<ListedTask> Tasks { get; } = [];
    [ObservableProperty] private bool _hasLists;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private double _fraction;
    /// <summary>"+2 more" when open tasks don't fit; "To-do" when no list is on; otherwise nothing. It opens the To-do screen.</summary>
    [ObservableProperty] private string? _footer;

    public TaskPanelViewModel(AppServices services, TodoViewModel todo, Action showTodo, TodoCadence cadence)
    {
        _services = services;
        _cadence = cadence;
        _todo = todo;
        _showTodo = showTodo;
        todo.Synced += Rebuild;
        Rebuild();
    }

    public string Heading => _cadence == TodoCadence.Daily ? "Daily tasks" : "Weekly tasks";

    [RelayCommand]
    void ShowTodo() => _showTodo();

    void Rebuild()
    {
        var lists = _services.Todos.Current.Lists.Where(l => !l.Deleted && l.Enabled && l.Cadence == _cadence).ToList();
        var (done, total) = Count(lists);
        HasLists = lists.Count > 0;
        Summary = $"{done}/{total}";
        Fraction = total == 0 ? 0 : (double)done / total;

        var open = new List<(string? ListName, TodoRowViewModel Row)>();
        foreach (var card in (_cadence == TodoCadence.Daily ? _todo.DailyOn : _todo.WeeklyOn))
            foreach (var row in card.Rows.Where(r => r.Checked != true))
                open.Add((lists.Count > 1 && open.All(t => t.ListName != card.Name) ? card.Name : null, row));
        // A task that stays keeps its line, so one that was opened stays open while its sub-tasks are ticked.
        Tasks.Sync(open.Take(MaxTasks), (task, next) => ReferenceEquals(task.Row, next.Row),
            next => new ListedTask(next.ListName, next.Row), (task, next) => task.ListName = next.ListName);
        Footer = !HasLists ? "To-do" : open.Count > MaxTasks ? $"+{open.Count - MaxTasks} more" : null;
    }

    static (int Done, int Total) Count(IEnumerable<TodoList> lists)
    {
        var progress = lists.Select(l => TodoOps.Progress(l.Rows)).ToList();
        return (progress.Sum(p => p.Done), progress.Sum(p => p.Total));
    }
}
