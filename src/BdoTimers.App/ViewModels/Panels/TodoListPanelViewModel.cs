using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class TodoListPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Guid _id;
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    List<TodoOutlineRow> _outline = [];
    bool _loading;
    bool _saving;
    bool _nameDirty;
    bool _rowsDirty;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _invalidName;
    [ObservableProperty] private Choice _enabled = Choice.OnOff[0];
    [ObservableProperty] private bool _confirmingDelete;

    public bool IsNew { get; }
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public ObservableCollection<TodoEditRowViewModel> Rows { get; } = [];
    public event Action<Guid>? FocusRequested;

    public TodoListPanelViewModel(AppServices services, IPanelHost host, Guid id, bool isNew = false)
    {
        _services = services;
        _host = host;
        _id = id;
        IsNew = isNew;
        _saveTimer.Tick += (_, _) => Flush();
        services.Todos.Changed += OnStoreChanged;
        Refresh();
    }

    void OnStoreChanged()
    {
        if (_saving) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var list = _services.Todos.Current.Lists.FirstOrDefault(l => l.Id == _id);
            if (list is null || list.Deleted) return;
            _outline = TodoOutline.MergeChecks(_outline, list.Rows);
            // The only outside row change while this modal panel is open is a reset. Keep unfinished rows and focus.
            _loading = true;
            if (!_nameDirty) Name = list.Name;
            Enabled = Choice.For(list.Enabled);
            _loading = false;
        });
    }

    void Refresh()
    {
        var list = _services.Todos.Current.Lists.FirstOrDefault(l => l.Id == _id);
        if (list is null || list.Deleted) return;
        _saveTimer.Stop();
        _nameDirty = false;
        _rowsDirty = false;
        _loading = true;
        Name = list.Name;
        InvalidName = false;
        Enabled = Choice.For(list.Enabled);
        _outline = TodoOutline.Flatten(list.Rows);
        RebuildRows();
        _loading = false;
    }

    void RebuildRows()
    {
        Rows.Clear();
        for (var i = 0; i < _outline.Count; i++)
            Rows.Add(new TodoEditRowViewModel(this, _outline[i],
                _outline[i].Level == 0 && i + 1 < _outline.Count && _outline[i + 1].Level == 1
                    ? CountChildren(i) : 0));
    }

    int CountChildren(int index)
    {
        var count = 0;
        while (index + count + 1 < _outline.Count && _outline[index + count + 1].Level == 1) count++;
        return count;
    }

    partial void OnNameChanged(string value)
    {
        if (_loading) return;
        InvalidName = string.IsNullOrWhiteSpace(value);
        _nameDirty = true;
        ArmSave();
    }

    partial void OnEnabledChanged(Choice value)
    {
        if (_loading) return;
        try
        {
            _saving = true;
            _services.Todos.SetEnabled(_id, value.IsOn);
        }
        catch (StateSaveException)
        {
            Refresh();
            throw;
        }
        finally { _saving = false; }
    }

    internal void UpdateText(Guid rowId, string text)
    {
        var index = _outline.FindIndex(row => row.Id == rowId);
        if (index < 0) return;
        _outline[index] = _outline[index] with { Text = text };
        _rowsDirty = true;
        ArmSave();
    }

    void ArmSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void Flush()
    {
        _saveTimer.Stop();
        try
        {
            _saving = true;
            if (_nameDirty && !InvalidName) _services.Todos.Rename(_id, Name);
            if (_rowsDirty) SaveRows();
            _nameDirty = false;
            _rowsDirty = false;
        }
        catch (StateSaveException)
        {
            Refresh();
            _nameDirty = _rowsDirty = false;
            throw;
        }
        finally { _saving = false; }
    }

    void SaveRows()
    {
        // A reset may have cleared checks while the editor was open; keep that newer completion state.
        var current = _services.Todos.Current.Lists.First(l => l.Id == _id);
        _outline = TodoOutline.MergeChecks(_outline, current.Rows);
        _services.Todos.ReplaceRows(_id, TodoOutline.Build(_outline));
    }

    void ChangeStructure(Action<List<TodoOutlineRow>> change, Guid? focus = null)
    {
        Flush();
        var next = _outline.ToList();
        change(next);
        if (next.SequenceEqual(_outline)) return;
        _outline = next;
        try
        {
            _saving = true;
            SaveRows();
            RebuildRows();
            if (focus is { } id) FocusRequested?.Invoke(id);
        }
        catch (StateSaveException)
        {
            Refresh();
            throw;
        }
        finally { _saving = false; }
    }

    [RelayCommand]
    void AddRow()
    {
        var id = Guid.NewGuid();
        ChangeStructure(rows => rows.Add(new TodoOutlineRow(id, "", false, 0)), id);
    }

    public void Enter(Guid id)
    {
        var newId = Guid.NewGuid();
        ChangeStructure(rows =>
        {
            var index = rows.FindIndex(r => r.Id == id);
            if (index < 0) return;
            var source = rows[index];
            var child = source.Level == 1 || index + 1 < rows.Count && rows[index + 1].Level == 1;
            rows.Insert(index + 1, new TodoOutlineRow(newId, "", false, child ? 1 : 0));
            if (source.Level == 0 && child) rows[index] = source with { Done = false };
        }, newId);
    }

    public bool Indent(Guid id)
    {
        var index = _outline.FindIndex(r => r.Id == id);
        if (index < 0) return false;
        var next = _outline.ToList();
        if (!TodoOutline.TryIndent(next, index)) return false;
        ChangeStructure(rows =>
        {
            TodoOutline.TryIndent(rows, index);
            var parentIndex = index - 1;
            while (parentIndex > 0 && rows[parentIndex].Level == 1) parentIndex--;
            rows[parentIndex] = rows[parentIndex] with { Done = false };
        }, id);
        return true;
    }

    public bool Outdent(Guid id)
    {
        var index = _outline.FindIndex(r => r.Id == id);
        var next = _outline.ToList();
        if (!TodoOutline.TryOutdent(next, index)) return false;
        ChangeStructure(rows => TodoOutline.TryOutdent(rows, index), id);
        return true;
    }

    public bool Backspace(Guid id)
    {
        var index = _outline.FindIndex(r => r.Id == id);
        if (index < 0 || !string.IsNullOrEmpty(_outline[index].Text) ||
            _outline[index].Level == 0 && CountChildren(index) > 0) return false;
        var focus = index > 0 ? _outline[index - 1].Id : (Guid?)null;
        ChangeStructure(rows => TodoOutline.Remove(rows, index), focus);
        return true;
    }

    public void Move(Guid id, int direction)
    {
        var index = _outline.FindIndex(r => r.Id == id);
        ChangeStructure(rows => TodoOutline.TryMove(rows, index, direction), id);
    }

    public bool CanMoveTo(Guid sourceId, Guid targetId, out bool below)
    {
        var source = _outline.FindIndex(r => r.Id == sourceId);
        var target = _outline.FindIndex(r => r.Id == targetId);
        below = false;
        if (source < 0 || target < 0 || source == target ||
            _outline[source].Level != _outline[target].Level || ParentId(source) != ParentId(target)) return false;
        below = source < target;
        return true;
    }

    public void MoveTo(Guid sourceId, Guid targetId)
    {
        if (!CanMoveTo(sourceId, targetId, out var below)) return;
        ChangeStructure(rows =>
        {
            var direction = below ? 1 : -1;
            for (var tries = 0; tries < rows.Count; tries++)
            {
                var from = rows.FindIndex(r => r.Id == sourceId);
                var to = rows.FindIndex(r => r.Id == targetId);
                if (direction > 0 ? from > to : from < to) break;
                if (!TodoOutline.TryMove(rows, from, direction)) break;
            }
        }, sourceId);
    }

    Guid? ParentId(int index)
    {
        if (_outline[index].Level == 0) return null;
        while (index > 0 && _outline[index].Level == 1) index--;
        return _outline[index].Id;
    }

    public void AddChild(Guid id)
    {
        var index = _outline.FindIndex(r => r.Id == id);
        if (index < 0 || _outline[index].Level != 0) return;
        var childId = Guid.NewGuid();
        ChangeStructure(rows =>
        {
            var at = rows.FindIndex(r => r.Id == id);
            var insert = at + 1;
            while (insert < rows.Count && rows[insert].Level == 1) insert++;
            rows.Insert(insert, new TodoOutlineRow(childId, "", false, 1));
            rows[at] = rows[at] with { Done = false };
        }, childId);
    }

    public void Remove(Guid id)
    {
        var index = _outline.FindIndex(r => r.Id == id);
        if (index >= 0) ChangeStructure(rows => TodoOutline.Remove(rows, index));
    }

    [RelayCommand] void AskDelete() => ConfirmingDelete = true;
    [RelayCommand] void CancelDelete() => ConfirmingDelete = false;

    [RelayCommand]
    void ConfirmDelete()
    {
        Flush();
        _services.Todos.Delete(_id);
        _host.ClosePanel();
    }

    public void OnClosed()
    {
        Flush();
        _services.Todos.Changed -= OnStoreChanged;
        if (IsNew && _services.Todos.Current.Lists.FirstOrDefault(l => l.Id == _id) is { } list &&
            list.Name == "New list" && list.Rows.Count == 0) _services.Todos.Delete(_id);
    }
}

public sealed partial class TodoEditRowViewModel : ObservableObject
{
    readonly TodoListPanelViewModel _owner;
    bool _loading;
    public Guid Id { get; }
    public int Level { get; }
    public Thickness Indent => new(Level * 24, 0, 0, 0);
    public bool CanAddChild => Level == 0;
    public int ChildCount { get; }
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _confirmingRemove;
    public IRelayCommand AddChildCommand { get; }
    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand ConfirmRemoveCommand { get; }
    public IRelayCommand CancelRemoveCommand { get; }

    public TodoEditRowViewModel(TodoListPanelViewModel owner, TodoOutlineRow row, int childCount)
    {
        _owner = owner;
        Id = row.Id;
        Level = row.Level;
        ChildCount = childCount;
        _loading = true;
        Text = row.Text;
        _loading = false;
        AddChildCommand = new RelayCommand(() => _owner.AddChild(Id));
        MoveUpCommand = new RelayCommand(() => _owner.Move(Id, -1));
        MoveDownCommand = new RelayCommand(() => _owner.Move(Id, 1));
        RemoveCommand = new RelayCommand(() => { if (ChildCount > 0) ConfirmingRemove = true; else _owner.Remove(Id); });
        ConfirmRemoveCommand = new RelayCommand(() => _owner.Remove(Id));
        CancelRemoveCommand = new RelayCommand(() => ConfirmingRemove = false);
    }

    partial void OnTextChanged(string value)
    {
        if (!_loading) _owner.UpdateText(Id, value);
    }
}
