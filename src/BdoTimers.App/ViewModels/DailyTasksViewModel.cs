using System.Collections.ObjectModel;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>A task of Today's panel; the first of a list shows the list's name when more than one list is on.</summary>
public sealed record DailyTask(string? ListName, TodoRowViewModel Row);

/// <summary>Today's panel of the active daily lists: what is still open, how far along they are, and the weekly count.</summary>
public sealed partial class DailyTasksViewModel : ObservableObject
{
    /// <summary>Open tasks the panel has room for.</summary>
    const int MaxTasks = 6;

    readonly AppServices _services;
    readonly TodoViewModel _todo;
    readonly Action _showTodo;

    public ObservableCollection<DailyTask> Tasks { get; } = [];
    [ObservableProperty] private bool _hasLists;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private double _fraction;
    /// <summary>"Weekly 1/16" while a weekly list is on; the line opens the To-do screen.</summary>
    [ObservableProperty] private string? _weekly;

    public DailyTasksViewModel(AppServices services, TodoViewModel todo, Action showTodo)
    {
        _services = services;
        _todo = todo;
        _showTodo = showTodo;
        todo.Synced += Rebuild;
        Rebuild();
    }

    [RelayCommand]
    void ShowTodo() => _showTodo();

    void Rebuild()
    {
        var lists = _services.Todos.Current.Lists.Where(l => !l.Deleted && l.Enabled).ToList();
        var daily = lists.Where(l => l.Cadence == TodoCadence.Daily).ToList();
        var (done, total) = Count(daily);
        HasLists = daily.Count > 0;
        Summary = $"{done}/{total}";
        Fraction = total == 0 ? 0 : (double)done / total;
        var (weeklyDone, weeklyTotal) = Count(lists.Where(l => l.Cadence == TodoCadence.Weekly));
        Weekly = weeklyTotal == 0 ? null : $"Weekly {weeklyDone}/{weeklyTotal}";

        var tasks = new List<DailyTask>();
        foreach (var card in _todo.DailyOn)
            foreach (var row in card.Rows.Where(r => r.Checked != true))
                tasks.Add(new DailyTask(daily.Count > 1 && tasks.All(t => t.ListName != card.Name) ? card.Name : null, row));
        Tasks.Clear();
        foreach (var task in tasks.Take(MaxTasks)) Tasks.Add(task);
    }

    static (int Done, int Total) Count(IEnumerable<TodoList> lists)
    {
        var progress = lists.Select(l => TodoOps.Progress(l.Rows)).ToList();
        return (progress.Sum(p => p.Done), progress.Sum(p => p.Total));
    }
}
