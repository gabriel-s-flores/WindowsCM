// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;

namespace WindowsCM.App;

// Shared prompt behind the popup EditItem/EditTitle chords (ticket 15
// deferred the dialogs to the settings phase; this is the v1 shape).
public partial class TextInputDialog : Window
{
    public TextInputDialog(string title, string initial, bool multiline)
    {
        InitializeComponent();
        Title = title;
        InputBox.Text = initial;
        InputBox.AcceptsReturn = multiline;
        InputBox.MinLines = multiline ? 3 : 1;
        InputBox.SelectAll();
    }

    public static string? Prompt(string title, string initial, bool multiline)
    {
        var dialog = new TextInputDialog(title, initial, multiline);
        return dialog.ShowDialog() == true ? dialog.InputBox.Text : null;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
