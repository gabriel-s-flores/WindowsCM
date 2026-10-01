// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Release;

namespace WindowsCM.App;

public partial class UpdateWindow : Window
{
    private readonly Func<Task> _install;
    private bool _busy;

    public UpdateWindow(AvailableUpdate update, Func<Task> install)
    {
        InitializeComponent();
        Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/WindowsCM;component/Assets/app.png"));
        VersionText.Text = update.Version.ToString();
        _install = install;
        Closing += (_, e) => e.Cancel = _busy;
    }

    private void LaterClick(object sender, RoutedEventArgs e) => Close();

    private async void InstallClick(object sender, RoutedEventArgs e)
    {
        _busy = true;
        InstallButton.IsEnabled = LaterButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Visible;
        try { await _install(); }
        catch (Exception ex)
        {
            ((App)System.Windows.Application.Current).LogError("update-install", ex);
            System.Windows.MessageBox.Show(this, LocalizationManager.Strings.UpdateFailed,
                LocalizationManager.Strings.UpdateTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
            InstallButton.IsEnabled = LaterButton.IsEnabled = true;
            StatusText.Visibility = Visibility.Collapsed;
        }
    }
}
