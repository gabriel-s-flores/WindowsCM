// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Popup;
using WindowsCM.Core.Transfer;

namespace WindowsCM.App;

public partial class QrWindow : Window
{
    private readonly App? _app;
    private string _currentPayload;
    private SharedItemSession? _session;

    // Standard constructor for plain string payloads
    public QrWindow(string payload)
    {
        InitializeComponent();
        _currentPayload = payload;
        PayloadText.Text = payload;
        QrImage.Source = Render(payload);
    }

    // Rich constructor for files, images, audio, and network transfers
    public QrWindow(SharedItemSession session, string url, App? app = null)
    {
        InitializeComponent();
        _session = session;
        _currentPayload = url;
        _app = app;

        UpdateWithSession(session, url);
    }

    private void UpdateWithSession(SharedItemSession session, string url)
    {
        _session = session;
        _currentPayload = url;
        var strings = LocalizationManager.Strings;

        HeaderTitle.Text = strings.QrHeaderDownloadTitle;
        HeaderSubtitle.Text = strings.QrHeaderDownloadSubtitle;

        FileMetaBorder.Visibility = Visibility.Visible;
        FileNameText.Text = session.FileName;
        FileSizeText.Text = session.FileSize > 0 ? $" • {FileDisplayHelper.FormatFileSize(session.FileSize)}" : "";
        FileIconGlyph.Text = ResolveGlyph(session.ContentType);

        PayloadText.Text = url;
        CopyLinkButton.Visibility = Visibility.Visible;

        QrImage.Source = Render(url);
    }

    private void OnCopyLinkClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPayload))
        {
            try
            {
                System.Windows.Clipboard.SetText(_currentPayload);
                SubtleToastWindow.ShowToast(LocalizationManager.Strings.ToastLinkCopied);
            }
            catch
            {
            }
        }
    }

    private void OnPickAnotherFileClicked(object sender, RoutedEventArgs e)
    {
        var strings = LocalizationManager.Strings;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = strings.QrFileDialogTitle,
            Filter = strings.QrFileDialogFilter
        };

        if (dlg.ShowDialog(this) == true && File.Exists(dlg.FileName))
        {
            var app = _app ?? (System.Windows.Application.Current as App);
            if (app?.TransferServer != null)
            {
                var newSession = app.TransferServer.RegisterShare(
                    title: Path.GetFileName(dlg.FileName),
                    kindLabel: strings.QrFallbackFileKind,
                    filePath: dlg.FileName);

                var ip = LocalNetworkResolver.GetPreferredLocalIp();
                var newUrl = app.TransferServer.BuildUrl(ip, $"/d/{newSession.Token}");
                UpdateWithSession(newSession, newUrl);
            }
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string ResolveGlyph(string contentType)
    {
        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return "\uEC4F"; // Audio
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "\uEB9F"; // Image
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "\uE714"; // Video
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return "\uE8A5"; // Doc
        return "\uED43"; // File
    }

    private static BitmapSource Render(string payload)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var code = new QRCode(data);
        using var bitmap = code.GetGraphic(20);
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
