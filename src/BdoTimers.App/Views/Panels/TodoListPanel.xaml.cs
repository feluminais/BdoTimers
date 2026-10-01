using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.Views.Panels;

public partial class TodoListPanel : UserControl
{
    TodoListPanelViewModel? _model;
    readonly DispatcherTimer _holdTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly DispatcherTimer _scrollTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    Grid? _pressedRow;
    Guid? _pressedId;
    Point _pressedAt;
    Point _grabAt;
    bool _dragArmed;
    Guid? _activeDrag;
    Guid? _dropTarget;
    Grid? _dragRow;
    Point _dragPointer;
    DragPreviewAdorner? _preview;
    AdornerLayer? _adornerLayer;

    public TodoListPanel()
    {
        InitializeComponent();
        _holdTimer.Tick += (_, _) => ArmDrag();
        _scrollTimer.Tick += (_, _) => ScrollNearEdge();
        PreviewMouseMove += Panel_PreviewMouseMove;
        PreviewMouseLeftButtonUp += Panel_PreviewMouseLeftButtonUp;
        LostMouseCapture += (_, e) => { if (e.OriginalSource == this) EndDrag(); };
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
            ClearPress();
            EndDrag();
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
        // With Alt held, WPF reports the arrow as Key.System and puts it in SystemKey.
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey is Key.Up or Key.Down)
        {
            _model.Move(id, e.SystemKey == Key.Up ? -1 : 1);
            e.Handled = true;
        }
    }

    void FocusRow(Guid id) => Dispatcher.BeginInvoke(() =>
    {
        var box = VisualTree.FindDescendant<TextBox>(RowsControl, b => b.Tag is Guid tag && tag == id);
        if (box is null) return;
        box.Focus();
        box.CaretIndex = box.Text.Length;
    }, DispatcherPriority.Loaded);

    void Row_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (e.ChangedButton != MouseButton.Left || sender is not Grid { Tag: Guid id } row ||
            VisualTree.FindAncestor<ButtonBase>(source, row) is not null) return;
        ClearPress();
        _pressedRow = row;
        _pressedId = id;
        _pressedAt = e.GetPosition(this);
        _grabAt = e.GetPosition(row);
        _dragArmed = VisualTree.FindAncestor<TextBox>(source, row) is null;
        if (!_dragArmed) _holdTimer.Start();
    }

    void ArmDrag()
    {
        _holdTimer.Stop();
        if (_pressedRow is null || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            ClearPress();
            return;
        }
        _dragArmed = true;
        _pressedRow.Background = (Brush)FindResource("AccentFillBrush");
        Mouse.OverrideCursor = Cursors.SizeAll;
    }

    void Panel_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_activeDrag is not null)
        {
            if (e.LeftButton != MouseButtonState.Pressed) EndDrag();
            else
            {
                _dragPointer = e.GetPosition(this);
                UpdateDropTarget();
            }
            e.Handled = true;
            return;
        }
        if (_pressedRow is not { } row || _pressedId is not { } id) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ClearPress();
            return;
        }
        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _pressedAt.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _pressedAt.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (!_dragArmed)
        {
            ClearPress(); // A quick drag in a text field still selects text.
            return;
        }
        var grab = _grabAt;
        ClearPress();
        BeginDrag(row, id, grab, point);
        e.Handled = true;
    }

    void Panel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeDrag is not { } source)
        {
            ClearPress();
            return;
        }

        _dragPointer = e.GetPosition(this);
        UpdateDropTarget();
        var target = _dropTarget;
        EndDrag();
        if (target is { } id) _model?.MoveTo(source, id);
        e.Handled = true;
    }

    void ClearPress()
    {
        _holdTimer.Stop();
        if (_pressedRow is not null) _pressedRow.Background = Brushes.Transparent;
        _pressedRow = null;
        _pressedId = null;
        _dragArmed = false;
        if (_activeDrag is null) Mouse.OverrideCursor = null;
    }

    void BeginDrag(Grid row, Guid id, Point grab, Point pointer)
    {
        var image = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(row.ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(row.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        image.Render(row);
        image.Freeze();
        _activeDrag = id;
        _dragRow = row;
        _dragPointer = pointer;
        _adornerLayer = AdornerLayer.GetAdornerLayer(this);
        if (_adornerLayer is not null)
        {
            _preview = new DragPreviewAdorner(this, image, grab,
                (Brush)FindResource("PanelBrush"), (Brush)FindResource("AccentBrush"));
            _adornerLayer.Add(_preview);
        }
        row.Opacity = 0.25;
        if (!CaptureMouse())
        {
            EndDrag();
            return;
        }
        Mouse.OverrideCursor = Cursors.SizeAll;
        _scrollTimer.Start();
        UpdateDropTarget();
    }

    void EndDrag()
    {
        if (_activeDrag is null) return;
        _activeDrag = null;
        _dropTarget = null;
        _scrollTimer.Stop();
        if (_dragRow is not null) _dragRow.Opacity = 1;
        _dragRow = null;
        if (_adornerLayer is not null && _preview is not null) _adornerLayer.Remove(_preview);
        _preview = null;
        _adornerLayer = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    void UpdateDropTarget()
    {
        Rect? line = null;
        _dropTarget = null;
        if (_activeDrag is { } source &&
            RowAt(TranslatePoint(_dragPointer, RowsControl)) is { Tag: Guid target } row &&
            _model is { } model && model.CanMoveTo(source, target, out var below))
        {
            var point = row.TransformToAncestor(this).Transform(new Point(0, below ? row.ActualHeight : 0));
            line = new Rect(point.X, point.Y, row.ActualWidth, 0);
            _dropTarget = target;
        }
        _preview?.Update(_dragPointer, line);
    }

    void ScrollNearEdge()
    {
        if (_activeDrag is null || RowsScrollViewer.ScrollableHeight <= 0) return;
        var point = TranslatePoint(_dragPointer, RowsScrollViewer);
        if (point.X < 0 || point.X > RowsScrollViewer.ActualWidth ||
            point.Y < 0 || point.Y > RowsScrollViewer.ViewportHeight) return;
        if (point.Y < 28) RowsScrollViewer.ScrollToVerticalOffset(RowsScrollViewer.VerticalOffset - 10);
        else if (point.Y > RowsScrollViewer.ViewportHeight - 28)
            RowsScrollViewer.ScrollToVerticalOffset(RowsScrollViewer.VerticalOffset + 10);
        else return;
        UpdateDropTarget();
    }

    Grid? RowAt(Point point) =>
        VisualTree.FindAncestor<Grid>(RowsControl.InputHitTest(point) as DependencyObject, RowsControl, row => row.Tag is Guid);

    sealed class DragPreviewAdorner : Adorner
    {
        readonly BitmapSource _image;
        readonly Point _grab;
        readonly Brush _background;
        readonly Brush _accent;
        Point _pointer;
        Rect? _line;

        public DragPreviewAdorner(UIElement adorned, BitmapSource image, Point grab, Brush background, Brush accent)
            : base(adorned)
        {
            _image = image;
            _grab = grab;
            _background = background;
            _accent = accent;
            IsHitTestVisible = false;
        }

        public void Update(Point pointer, Rect? line)
        {
            _pointer = pointer;
            _line = line;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            if (_line is { } line)
                drawing.DrawLine(new Pen(_accent, 2), new Point(line.Left, line.Top), new Point(line.Right, line.Top));
            var rect = new Rect(_pointer.X - _grab.X, _pointer.Y - _grab.Y,
                _image.Width, _image.Height);
            drawing.PushOpacity(0.94);
            drawing.DrawRoundedRectangle(_background, null, rect, 2, 2);
            drawing.DrawImage(_image, rect);
            drawing.DrawRoundedRectangle(null, new Pen(_accent, 1), rect, 2, 2);
            drawing.Pop();
        }
    }
}
