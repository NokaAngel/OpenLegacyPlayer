using System.Windows;
using System.Windows.Input;

namespace OpenLegacyPlayer.Views;

public partial class InputDialog : Window
{
    public string ResponseText { get; private set; } = string.Empty;

    public InputDialog(string title, string prompt, string initial = "")
    {
        InitializeComponent();
        TitleText.Text = title;
        PromptText.Text = prompt;
        InputBox.Text = initial;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResponseText = InputBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { ResponseText = InputBox.Text; DialogResult = true; }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    /// <summary>Shows the dialog and returns the trimmed text, or null if cancelled/empty.</summary>
    public static string? Prompt(Window owner, string title, string prompt, string initial = "")
    {
        var dialog = new InputDialog(title, prompt, initial) { Owner = owner };
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText)
            ? dialog.ResponseText.Trim()
            : null;
    }
}
