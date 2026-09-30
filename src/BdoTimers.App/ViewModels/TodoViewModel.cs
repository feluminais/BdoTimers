using System.Collections.ObjectModel;
using System.Globalization;
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
        services.UiClock.Tick += _ => UpdateResetLabels();
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

    void Open(Guid id) => _host.OpenPanel(new TodoListPanelViewModel(_services, _host, id));

    void Toggle(Guid listId, Guid rowId)
    {
        _focusRow = rowId;
        _services.Todos.Toggle(listId, rowId);
    }

    void UpdateResetLabels()
    {
        var now = DateTimeOffset.UtcNow;
        var settings = _services.Settings.Current;
        WeeklyResetLabel = "Resets " + TodoReset.Next(settings.WeeklyTodoReset with { Cadence = TodoCadence.Weekly }, now)
            .ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture);
        DailyResetLabel = "Resets " + TodoReset.Next(settings.DailyTodoReset with { Cadence = TodoCadence.Daily }, now)
            .ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    void Sync()
    {
        var lists = _services.Todos.Current.Lists.Where(list => !list.Deleted).ToList();
        SyncCollection(Weekly, lists.Where(l => l.Cadence == TodoCadence.Weekly).ToList());
        SyncCollection(Daily, lists.Where(l => l.Cadence == TodoCadence.Daily).ToList());
        if (_focusRow is { } id)
        {
            _focusRow = null;
            RowSynced?.Invoke(id);
        }
    }

    void SyncCollection(ObservableCollection<TodoListCardViewModel> cards, IReadOnlyList<TodoList> lists)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            var old = cards.FirstOrDefault(c => c.Id == lists[i].Id);
            if (old is null) cards.Insert(i, new TodoListCardViewModel(lists[i], Open, Toggle, SetEnabled));
            else
            {
                var at = cards.IndexOf(old);
                if (at != i) cards.Move(at, i);
                old.Update(lists[i]);
            }
        }
        while (cards.Count > lists.Count) cards.RemoveAt(cards.Count - 1);
    }

    void SetEnabled(Guid id, bool enabled) => _services.Todos.SetEnabled(id, enabled);
}

public sealed partial class TodoListCardViewModel : ObservableObject
{
    readonly Action<Guid> _open;
    readonly Action<Guid, Guid> _toggle;
    readonly Action<Guid, bool> _setEnabled;
    bool _syncingEnabled;
    public Guid Id { get; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isOn;
    public ObservableCollection<TodoRowViewModel> Rows { get; } = [];
    public IRelayCommand OpenCommand { get; }

    public TodoListCardViewModel(TodoList list, Action<Guid> open, Action<Guid, Guid> toggle, Action<Guid, bool> setEnabled)
    {
        Id = list.Id;
        _open = open;
        _toggle = toggle;
        _setEnabled = setEnabled;
        OpenCommand = new RelayCommand(() => _open(Id));
        Update(list);
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_syncingEnabled) return;
        try { _setEnabled(Id, value); }
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
        TodoRowViewModel.Sync(Rows, TodoOps.OpenFirst(list.Rows), list.Enabled, Id, _open, _toggle);
    }
}

public sealed partial class TodoRowViewModel : ObservableObject
{
    readonly Guid _listId;
    readonly Action<Guid> _open;
    readonly Action<Guid, Guid> _toggle;
    public Guid Id { get; }
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _automationName = "";
    [ObservableProperty] private bool? _checked;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isStruck;
    [ObservableProperty] private string _partialProgress = "";
    public ObservableCollection<TodoRowViewModel> Children { get; } = [];
    public IRelayCommand ToggleCommand { get; }
    public IRelayCommand OpenCommand { get; }

    public TodoRowViewModel(TodoRow row, bool enabled, Guid listId, Action<Guid> open, Action<Guid, Guid> toggle)
    {
        Id = row.Id;
        _listId = listId;
        _open = open;
        _toggle = toggle;
        ToggleCommand = new RelayCommand(() => _toggle(_listId, Id));
        OpenCommand = new RelayCommand(() => _open(_listId));
        Update(row, enabled);
    }

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
        Sync(Children, TodoOps.OpenFirst(row.Children), enabled, _listId, _open, _toggle);
    }

    public static void Sync(ObservableCollection<TodoRowViewModel> target, IReadOnlyList<TodoRow> rows,
        bool enabled, Guid listId, Action<Guid> open, Action<Guid, Guid> toggle)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var old = target.FirstOrDefault(vm => vm.Id == rows[i].Id);
            if (old is null) target.Insert(i, new TodoRowViewModel(rows[i], enabled, listId, open, toggle));
            else
            {
                var at = target.IndexOf(old);
                if (at != i) target.Move(at, i);
                old.Update(rows[i], enabled);
            }
        }
        while (target.Count > rows.Count) target.RemoveAt(target.Count - 1);
    }
}
