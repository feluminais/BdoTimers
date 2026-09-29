using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.Views.Panels;

public partial class TodoListPanel : UserControl
{
    TodoListPanelViewModel? _model;
    Point _dragStart;
    Guid? _dragId;

    public TodoListPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null) _model.FocusRequested -= FocusRow;
            _model = DataContext as TodoListPanelViewModel;
            if (IsLoaded && _model is not null) _model.FocusRequested += FocusRow;
        };
        Loaded += (_, _) =>
        {
            if (_model is not null) _model.FocusRequested += FocusRow;
            if (_model?.IsNew == true) { NameBox.Focus(); NameBox.SelectAll(); }
        };
        Unloaded += (_, _) =>
        {
            if (_model is not null) _model.FocusRequested -= FocusRow;
        };
    }

    void RowText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_model is null || sender is not TextBox { Tag: Guid id } box) return;
        var shift = Keyboard.Modifiers == ModifierKeys.Shift;
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            _model.Enter(id);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && (Keyboard.Modifiers == ModifierKeys.None || shift))
            e.Handled = shift ? _model.Outdent(id) : _model.Indent(id);
        else if (e.Key == Key.Back && box.Text.Length == 0 && Keyboard.Modifiers == ModifierKeys.None)
            e.Handled = _model.Backspace(id);
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key is Key.Up or Key.Down)
        {
            _model.Move(id, e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }
    }

    void FocusRow(Guid id) => Dispatcher.BeginInvoke(() =>
    {
        var box = FindRowBox(RowsControl, id);
        if (box is null) return;
        box.Focus();
        box.CaretIndex = box.Text.Length;
    }, DispatcherPriority.Loaded);

    static TextBox? FindRowBox(DependencyObject root, Guid id)
    {
        if (root is TextBox { Tag: Guid tag } box && tag == id) return box;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindRowBox(VisualTreeHelper.GetChild(root, i), id);
            if (found is not null) return found;
        }
        return null;
    }

    void Grip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Guid id })
        {
            _dragId = id;
            _dragStart = e.GetPosition(this);
        }
    }

    void Grip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragId is not { } id || e.LeftButton != MouseButtonState.Pressed) return;
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragId = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(Guid), id), DragDropEffects.Move);
    }

    void Row_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(Guid)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    void Row_Drop(object sender, DragEventArgs e)
    {
        if (_model is not null && sender is FrameworkElement { Tag: Guid target } &&
            e.Data.GetData(typeof(Guid)) is Guid source) _model.MoveTo(source, target);
        e.Handled = true;
    }
}
