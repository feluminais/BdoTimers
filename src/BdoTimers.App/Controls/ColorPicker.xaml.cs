using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Controls;

/// <summary>A saturation / brightness square, a hue strip and a hex field.</summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorPicker),
        new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

    // Kept apart from Color so a grey or black keeps the hue it was picked on.
    HsvColor _hsv;
    bool _setting;

    public ColorPicker()
    {
        InitializeComponent();
        Refresh();
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (ColorPicker)d;
        if (picker._setting) return;
        picker._hsv = HsvColor.From(Rgb((Color)e.NewValue));
        picker.Refresh();
    }

    void Apply(HsvColor hsv)
    {
        _hsv = hsv;
        var rgb = hsv.ToRgb();
        _setting = true;
        SetCurrentValue(ColorProperty, Color.FromRgb(rgb.R, rgb.G, rgb.B));
        _setting = false;
        Refresh();
    }

    void Refresh()
    {
        var hue = new HsvColor(_hsv.H, 1, 1).ToRgb();
        HueFill.Fill = new SolidColorBrush(Color.FromRgb(hue.R, hue.G, hue.B));
        Canvas.SetLeft(SquareThumb, _hsv.S * Square.Width - SquareThumb.Width / 2);
        Canvas.SetTop(SquareThumb, (1 - _hsv.V) * Square.Height - SquareThumb.Height / 2);
        Canvas.SetTop(HueThumb, _hsv.H / 360 * HueStrip.Height - HueThumb.Height / 2);
        // Picking in the square can leave focus in Hex; keep it in sync so losing focus cannot restore stale text.
        Hex.Text = Rgb(Color).ToHex();
    }

    static RgbColor Rgb(Color c) => new(c.R, c.G, c.B);

    void Square_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Square.CaptureMouse();
        PickSquare(e.GetPosition(Square));
    }

    void Square_MouseMove(object sender, MouseEventArgs e)
    {
        if (Square.IsMouseCaptured) PickSquare(e.GetPosition(Square));
    }

    void Hue_MouseDown(object sender, MouseButtonEventArgs e)
    {
        HueStrip.CaptureMouse();
        PickHue(e.GetPosition(HueStrip));
    }

    void Hue_MouseMove(object sender, MouseEventArgs e)
    {
        if (HueStrip.IsMouseCaptured) PickHue(e.GetPosition(HueStrip));
    }

    void Drag_MouseUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();

    void PickSquare(Point p) =>
        Apply(_hsv with { S = Math.Clamp(p.X / Square.Width, 0, 1), V = 1 - Math.Clamp(p.Y / Square.Height, 0, 1) });

    void PickHue(Point p) => Apply(_hsv with { H = Math.Clamp(p.Y / HueStrip.Height, 0, 1) * 360 });

    void Hex_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ApplyHex();
        e.Handled = true;
    }

    void Hex_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyHex();

    void Hex_TextChanged(object sender, TextChangedEventArgs e) => Ui.SetHasError(Hex, false);

    void ApplyHex()
    {
        if (!RgbColor.TryParseHex(Hex.Text, out var rgb))
        {
            Ui.SetHasError(Hex, true);
            return;
        }
        Apply(HsvColor.From(rgb));
        Hex.Text = rgb.ToHex();
    }
}
