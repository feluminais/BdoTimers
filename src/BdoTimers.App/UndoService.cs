using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App;

public sealed partial class UndoService : ObservableObject, IDisposable
{
    readonly UndoHistory _history;

    public UndoService(TimerStore timers, TodoStore todos, IClock clock, Action<string>? releaseImage = null)
    {
        _history = new(timers, todos, clock, releaseImage);
        _history.Changed += OnHistoryChanged;
    }

    public bool CanUndo => _history.CanUndo;
    public string Message => _history.Message;
    public bool DeleteTimer(Guid id) => _history.DeleteTimer(id);
    public bool ResetTimer(Guid id) => _history.ResetTimer(id);
    public bool DeleteTodoList(Guid id) => _history.DeleteTodoList(id);
    public void Refresh() => _history.Refresh();
    public void ReleasePicture(string? image) => _history.ReleasePicture(image);

    [RelayCommand(CanExecute = nameof(CanUndo))]
    void Undo() => _history.TryUndo();

    [RelayCommand]
    void Dismiss() => _history.Dismiss();

    void OnHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(Message));
        UndoCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _history.Changed -= OnHistoryChanged;
        _history.Dispose();
    }
}
