using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        for (DependencyObject? current = e.OriginalSource as DependencyObject;
             current is not null && current != sender;
             current = current is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is ButtonBase) return;
        if (sender is Border { DataContext: TodoListCardViewModel list }) list.OpenCommand.Execute(null);
    }

    void FocusRow(Guid id) => Dispatcher.BeginInvoke(() =>
    {
        var box = FindBox(this, id);
        box?.Focus();
    }, DispatcherPriority.Loaded);

    static CheckBox? FindBox(DependencyObject root, Guid id)
    {
        if (root is CheckBox { Tag: Guid tag } box && tag == id) return box;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var found = FindBox(VisualTreeHelper.GetChild(root, i), id);
            if (found is not null) return found;
        }
        return null;
    }
}
