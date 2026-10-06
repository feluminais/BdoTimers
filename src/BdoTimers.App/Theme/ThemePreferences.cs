using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Theme;

/// <summary>Applies Windows contrast and text preferences without restarting the app.</summary>
public sealed class ThemePreferences : IDisposable
{
    readonly Application _application;
    readonly PersistentState<AppSettings> _settings;
    readonly Dictionary<string, Brush> _palette = [];
    readonly ResourceDictionary _theme;
    bool? _highContrast;
    double _scale;
    bool _disposed;

    ThemePreferences(Application application, PersistentState<AppSettings> settings)
    {
        _application = application;
        _settings = settings;
        _theme = application.Resources.MergedDictionaries.First(d => d.Contains("BgBrush"));
        foreach (var key in PaletteKeys) _palette[key] = (Brush)_theme[key];
        SystemParameters.StaticPropertyChanged += SystemParametersChanged;
        SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
        settings.Changed += ApplyOnDispatcher;
        Apply();
    }

    public static ThemePreferences Initialize(Application application, PersistentState<AppSettings> settings) =>
        new(application, settings);

    /// <summary>Every brush of Tokens.xaml: High Contrast replaces each with a system colour.</summary>
    internal static readonly string[] PaletteKeys =
    [
        "BgBrush", "CardBrush", "PanelBrush", "RaisedBrush", "ControlFillBrush", "HairlineBrush", "HairlineStrongBrush",
        "HairlineHoverBrush", "HoverFillBrush", "HoverTextBrush", "TextBrush", "SubtleBrush", "PastBrush", "AccentBrush",
        "AccentSoftBrush", "AccentFillBrush", "AccentTextBrush", "DangerBrush", "DangerFillBrush", "DimBrush", "FocusBrush",
        "UpdateBrush", "AmberBrush", "EmberBrush", "GoodBrush", "TimersAccentBrush", "BronzeBrush", "OnBronzeBrush",
        "IndigoCardBrush", "IndigoRaisedBrush", "IndigoLineBrush", "IndigoLineStrongBrush", "IndigoLineHoverBrush",
        "IndigoHoverFillBrush", "ForestCardBrush", "ForestRaisedBrush", "ForestLineBrush", "ForestLineStrongBrush",
        "ForestLineHoverBrush", "ForestHoverFillBrush",
    ];

    void SystemParametersChanged(object? sender, PropertyChangedEventArgs e) => RefreshSystemPreferences();
    void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => RefreshSystemPreferences();

    void RefreshSystemPreferences()
    {
        if (_disposed || _application.Dispatcher.HasShutdownStarted) return;
        void Refresh() { _highContrast = null; Apply(); }
        if (_application.Dispatcher.CheckAccess()) Refresh();
        else _application.Dispatcher.BeginInvoke(Refresh);
    }

    void ApplyOnDispatcher()
    {
        if (_disposed || _application.Dispatcher.HasShutdownStarted) return;
        if (_application.Dispatcher.CheckAccess()) Apply();
        else _application.Dispatcher.BeginInvoke(Apply);
    }

    void Apply()
    {
        if (_disposed) return;
        var contrast = SystemParameters.HighContrast;
        if (_highContrast != contrast)
        {
            foreach (var key in PaletteKeys) _theme[key] = contrast ? ContrastBrush(key) : _palette[key];
            _highContrast = contrast;
        }
        var userScale = _settings.Current.TextScale;
        if (!double.IsFinite(userScale)) userScale = 1;
        var scale = WindowsTextScale() * Math.Clamp(userScale, 1, 1.5);
        if (Math.Abs(_scale - scale) > .001)
        {
            _application.Resources["UiScaleTransform"] = new ScaleTransform(scale, scale);
            _scale = scale;
        }
    }

    static Brush ContrastBrush(string key) => key switch
    {
        "BgBrush" or "PanelBrush" or "DimBrush" or "CardBrush" or "RaisedBrush" or "ControlFillBrush"
            or "IndigoCardBrush" or "IndigoRaisedBrush" or "ForestCardBrush" or "ForestRaisedBrush" => SystemColors.WindowBrush,
        "HoverFillBrush" or "AccentFillBrush" or "DangerFillBrush" or "IndigoHoverFillBrush" or "ForestHoverFillBrush"
            or "BronzeBrush" => SystemColors.HighlightBrush,
        "HoverTextBrush" or "OnBronzeBrush" => SystemColors.HighlightTextBrush,
        "FocusBrush" or "AccentBrush" or "AccentSoftBrush" or "TimersAccentBrush" => SystemColors.HighlightBrush,
        _ => SystemColors.WindowTextBrush,
    };

    static double WindowsTextScale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
            return key?.GetValue("TextScaleFactor") is int percent ? Math.Clamp(percent / 100d, 1, 2.25) : 1;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            Log.Error("Couldn't read Windows text size", ex);
            return 1;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        SystemParameters.StaticPropertyChanged -= SystemParametersChanged;
        SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
        _settings.Changed -= ApplyOnDispatcher;
    }
}
