using System.Collections.ObjectModel;
using System.Windows;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class TodoViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    Guid? _focusRow;

    [ObservableProperty] private string _weeklyResetLabel = "";
    [ObservableProperty] private string _dailyResetLabel = "";
    public ObservableCollection<TodoListCardViewModel> Weekly { get; } = [];
    public ObservableCollection<TodoListCardViewModel> Daily { get; } = [];
    public event Action<Guid>? RowSynced;

    public TodoViewModel(AppServices services, IPanelHost host)
    {
        _services = services;
        _host = host;
        services.Todos.Changed += () => Application.Current.Dispatcher.BeginInvoke(Sync);
        services.Settings.Changed += () => Application.Current.Dispatcher.BeginInvoke(UpdateResetLabels);
        Sync();
        UpdateResetLabels();
    }

    [RelayCommand] void NewWeekly() => NewList(TodoCadence.Weekly);
    [RelayCommand] void NewDaily() => NewList(TodoCadence.Daily);

    void NewList(TodoCadence cadence)
    {
        var id = _services.Todos.CreateList(cadence, _services.Settings.Current);
        _host.OpenPanel(new TodoListPanelViewModel(_services, _host, id, isNew: true));
    }

    internal void Open(Guid id) => _host.OpenPanel(new TodoListPanelViewModel(_services, _host, id));

    internal void Toggle(Guid listId, Guid rowId)
    {
        _focusRow = rowId;
        _services.Todos.Toggle(listId, rowId);
    }

    internal void SetEnabled(Guid id, bool enabled) => _services.Todos.SetEnabled(id, enabled);

    public void UpdateResetLabels()
    {
        var now = DateTimeOffset.UtcNow;
        var settings = _services.Settings.Current;
        WeeklyResetLabel = "Resets " + Formats.DayTime(TodoReset.Next(settings.WeeklyTodoReset with { Cadence = TodoCadence.Weekly }, now));
        DailyResetLabel = "Resets " + Formats.Time(TodoReset.Next(settings.DailyTodoReset with { Cadence = TodoCadence.Daily }, now));
    }

    void Sync()
    {
        var lists = _services.Todos.Current.Lists.Where(list => !list.Deleted).ToList();
        SyncCards(Weekly, lists.Where(l => l.Cadence == TodoCadence.Weekly));
        SyncCards(Daily, lists.Where(l => l.Cadence == TodoCadence.Daily));
        if (_focusRow is { } id)
        {
            _focusRow = null;
            RowSynced?.Invoke(id);
        }
    }

    void SyncCards(ObservableCollection<TodoListCardViewModel> cards, IEnumerable<TodoList> lists) =>
        cards.Sync(lists, (card, list) => card.Id == list.Id, list => new TodoListCardViewModel(this, list),
            (card, list) => card.Update(list));
}

public sealed partial class TodoListCardViewModel : ObservableObject
{
    readonly TodoViewModel _owner;
    bool _syncingEnabled;
    public Guid Id { get; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isOn;
    public ObservableCollection<TodoRowViewModel> Rows { get; } = [];

    public TodoListCardViewModel(TodoViewModel owner, TodoList list)
    {
        _owner = owner;
        Id = list.Id;
        Update(list);
    }

    [RelayCommand]
    void Open() => _owner.Open(Id);

    partial void OnIsOnChanged(bool value)
    {
        if (_syncingEnabled) return;
        try { _owner.SetEnabled(Id, value); }
        catch
        {
            _syncingEnabled = true;
            IsOn = !value;
            _syncingEnabled = false;
            throw;
        }
    }

    public void Update(TodoList list)
    {
        Name = list.Name;
        _syncingEnabled = true;
        IsOn = list.Enabled;
        _syncingEnabled = false;
        var (done, total) = TodoOps.Progress(list.Rows);
        Summary = $"{done}/{total}";
        TodoRowViewModel.Sync(Rows, TodoOps.OpenFirst(list.Rows), list.Enabled, _owner, Id);
    }
}

public sealed partial class TodoRowViewModel : ObservableObject
{
    readonly TodoViewModel _owner;
    readonly Guid _listId;
    public Guid Id { get; }
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _automationName = "";
    [ObservableProperty] private bool? _checked;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isStruck;
    [ObservableProperty] private string _partialProgress = "";
    public ObservableCollection<TodoRowViewModel> Children { get; } = [];

    public TodoRowViewModel(TodoViewModel owner, Guid listId, TodoRow row, bool enabled)
    {
        _owner = owner;
        _listId = listId;
        Id = row.Id;
        Update(row, enabled);
    }

    [RelayCommand]
    void Toggle() => _owner.Toggle(_listId, Id);

    [RelayCommand]
    void Open() => _owner.Open(_listId);

    public void Update(TodoRow row, bool enabled)
    {
        Text = row.Text;
        Enabled = enabled;
        var check = TodoOps.Check(row);
        Checked = check == TodoCheck.Partial ? null : check == TodoCheck.Done;
        IsStruck = !enabled || check == TodoCheck.Done;
        var (done, total) = TodoOps.Progress(row.Children);
        PartialProgress = check == TodoCheck.Partial ? $"{done}/{total}" : "";
        AutomationName = enabled ? row.Text : row.Text + ", off";
        Sync(Children, TodoOps.OpenFirst(row.Children), enabled, _owner, _listId);
    }

    internal static void Sync(ObservableCollection<TodoRowViewModel> target, IEnumerable<TodoRow> rows, bool enabled,
        TodoViewModel owner, Guid listId) =>
        target.Sync(rows, (vm, row) => vm.Id == row.Id, row => new TodoRowViewModel(owner, listId, row, enabled),
            (vm, row) => vm.Update(row, enabled));
}
