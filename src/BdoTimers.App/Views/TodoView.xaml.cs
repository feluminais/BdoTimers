using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;

namespace BdoTimers.App.Views;

public partial class TodoView : UserControl
{
    TodoViewModel? _model;

    public TodoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null) _model.RowSynced -= FocusRow;
            _model = DataContext as TodoViewModel;
            if (IsLoaded && _model is not null) _model.RowSynced += FocusRow;
        };
        Unloaded += (_, _) =>
        {
            if (_model is not null) _model.RowSynced -= FocusRow;
        };
        Loaded += (_, _) =>
        {
            if (_model is not null) _model.RowSynced += FocusRow;
        };
    }

    void List_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: TodoListCardViewModel list } card
            || VisualTree.FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject, card) is not null) return;
        list.OpenCommand.Execute(null);
    }

    void FocusRow(Guid id) => Dispatcher.BeginInvoke(
        () => VisualTree.FindDescendant<CheckBox>(this, box => box.Tag is Guid tag && tag == id)?.Focus(),
        DispatcherPriority.Loaded);
}
