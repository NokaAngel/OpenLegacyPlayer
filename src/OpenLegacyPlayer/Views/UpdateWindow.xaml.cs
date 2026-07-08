using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenLegacyPlayer.Services;

namespace OpenLegacyPlayer.Views;

/// <summary>
/// Non-intrusive "an update is available" dialog: it shows the new version and
/// the release changelog, and lets the user choose <b>Update now</b> or
/// <b>Ask later</b> — never forcing anything. Returns true from
/// <see cref="Ask"/> when the user wants to proceed.
/// </summary>
public partial class UpdateWindow : Window
{
    public UpdateWindow(UpdateResult update, bool canAutoUpdate)
    {
        InitializeComponent();

        HeadlineText.Text = $"OpenLegacy Player {update.LatestVersion} is available";
        SubHeadText.Text = $"You have version {UpdateService.CurrentVersion.ToString(3)}";

        if (canAutoUpdate)
        {
            UpdateButton.Content = "Update now";
            HintText.Text = "The update will download and install itself, then reopen the app.";
        }
        else
        {
            UpdateButton.Content = "Download…";
            HintText.Text = "This opens the download page in your browser.";
        }

        RenderNotes(update.Notes);
    }

    private void Update_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Later_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    /// <summary>Shows the dialog; returns true if the user chose to update/download.</summary>
    public static bool Ask(Window owner, UpdateResult update, bool canAutoUpdate)
    {
        var dialog = new UpdateWindow(update, canAutoUpdate) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    // --- Lightweight markdown → styled text blocks --------------------------

    private static readonly Brush HeaderBrush =
        (Brush)Application.Current.FindResource("HeaderTextBrush");
    private static readonly Brush BodyBrush =
        (Brush)Application.Current.FindResource("BodyTextBrush");
    private static readonly Brush AccentBrush =
        (Brush)Application.Current.FindResource("AccentLineBrush");

    private void RenderNotes(string? notes)
    {
        NotesPanel.Children.Clear();

        if (string.IsNullOrWhiteSpace(notes))
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = "No release notes were provided for this version.",
                Foreground = (Brush)Application.Current.FindResource("SubtleTextBrush"),
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var raw in notes.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.TrimEnd();

            if (line.Length == 0)
            {
                NotesPanel.Children.Add(new TextBlock { Height = 6 });
                continue;
            }

            // Headings (#, ##, ###)
            var heading = Regex.Match(line, @"^(#{1,3})\s+(.*)$");
            if (heading.Success)
            {
                NotesPanel.Children.Add(new TextBlock
                {
                    Text = Clean(heading.Groups[2].Value),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = heading.Groups[1].Value.Length == 1 ? 13.5 : 12.5,
                    Foreground = HeaderBrush,
                    Margin = new Thickness(0, NotesPanel.Children.Count > 0 ? 6 : 0, 0, 3),
                    TextWrapping = TextWrapping.Wrap
                });
                continue;
            }

            // Bullet items (- or *)
            var bullet = Regex.Match(line, @"^\s*[-*]\s+(.*)$");
            if (bullet.Success)
            {
                NotesPanel.Children.Add(BuildBullet(Clean(bullet.Groups[1].Value)));
                continue;
            }

            // Plain paragraph
            NotesPanel.Children.Add(new TextBlock
            {
                Text = Clean(line),
                Foreground = BodyBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }
    }

    private Panel BuildBullet(string text)
    {
        var row = new Grid { Margin = new Thickness(2, 1, 0, 1) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        row.ColumnDefinitions.Add(new ColumnDefinition());

        var dot = new TextBlock
        {
            Text = "•",
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(dot, 0);

        var body = new TextBlock
        {
            Text = text,
            Foreground = BodyBrush,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(body, 1);

        row.Children.Add(dot);
        row.Children.Add(body);
        return row;
    }

    /// <summary>Strips the inline markdown GitHub notes commonly contain.</summary>
    private static string Clean(string s)
    {
        s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1"); // [text](url) -> text
        s = Regex.Replace(s, @"[*_]{1,3}([^*_]+)[*_]{1,3}", "$1"); // **bold**/_em_ -> text
        s = s.Replace("`", "");
        return s.Trim();
    }
}
