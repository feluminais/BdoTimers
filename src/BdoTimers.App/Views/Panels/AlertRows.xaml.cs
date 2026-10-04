using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BdoTimers.App.Views.Panels;

public partial class AlertRows : UserControl
{
    public AlertRows() => InitializeComponent();

    void Generate_Click(object sender, RoutedEventArgs e) => PanelEdits.Complete(this);

    /// <summary>Opening the editor puts the caret at the end, so typing or + inserts continue the line.</summary>
    void CustomVoiceLine_Click(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            VoiceLineBox.Focus();
            VoiceLineBox.CaretIndex = VoiceLineBox.Text.Length;
        });

    void InsertName_Click(object sender, RoutedEventArgs e) => Insert("[name]");

    void InsertTime_Click(object sender, RoutedEventArgs e) => Insert("[time]");

    /// <summary>Types the placeholder at the caret (replacing any selection), with a space before it when it would
    /// otherwise stick to the previous word.</summary>
    void Insert(string placeholder)
    {
        var start = VoiceLineBox.SelectionStart;
        var text = start > 0 && !char.IsWhiteSpace(VoiceLineBox.Text[start - 1]) ? " " + placeholder : placeholder;
        VoiceLineBox.SelectedText = text;
        VoiceLineBox.CaretIndex = start + text.Length;
        VoiceLineBox.Focus();
    }
}
