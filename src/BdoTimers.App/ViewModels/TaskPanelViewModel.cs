using System.Collections.ObjectModel;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>A task of a Today panel; the first of a list shows the list's name when more than one list is on.</summary>
public sealed record ListedTask(string? ListName, TodoRowViewModel Row);

/// <summary>
/// One of Today's task panels, Daily or Weekly: what is still open in the active lists of its cadence (a task with
/// sub-tasks is one line with how far along it is), how far along they are, and a way on to the To-do screen.
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

        var tasks = new List<ListedTask>();
        foreach (var card in (_cadence == TodoCadence.Daily ? _todo.DailyOn : _todo.WeeklyOn))
            foreach (var row in card.Rows.Where(r => r.Checked != true))
                tasks.Add(new ListedTask(lists.Count > 1 && tasks.All(t => t.ListName != card.Name) ? card.Name : null, row));
        Tasks.Clear();
        foreach (var task in tasks.Take(MaxTasks)) Tasks.Add(task);
        Footer = !HasLists ? "To-do" : tasks.Count > MaxTasks ? $"+{tasks.Count - MaxTasks} more" : null;
    }

    static (int Done, int Total) Count(IEnumerable<TodoList> lists)
    {
        var progress = lists.Select(l => TodoOps.Progress(l.Rows)).ToList();
        return (progress.Sum(p => p.Done), progress.Sum(p => p.Total));
    }
}
