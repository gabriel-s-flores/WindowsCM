// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using WindowsCM.Core.Localization;
using WindowsCM.Core.Transfer;

namespace WindowsCM.App;

public partial class MobileTransferWindow : Window
{
    private readonly MiniTransferHttpServer _server;
    private readonly string _transferUrl;

    public MobileTransferWindow(MiniTransferHttpServer server, IPAddress localIp)
    {
        InitializeComponent();
        _server = server;
        _transferUrl = _server.BuildUrl(localIp, "/");

        UrlText.Text = _transferUrl;
        QrImage.Source = RenderQrCode(_transferUrl);

        _server.PayloadReceived += OnPayloadReceived;
        Closed += (_, _) => _server.PayloadReceived -= OnPayloadReceived;
    }

    private void OnPayloadReceived(IncomingTransferPayload payload)
    {
        Dispatcher.Invoke(() =>
        {
            var strings = LocalizationManager.Strings;
            StatusDot.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
            if (!string.IsNullOrWhiteSpace(payload.Text))
            {
                var excerpt = payload.Text.Length > 40 ? payload.Text[..37] + "..." : payload.Text;
                StatusText.Text = strings.MobileTextReceived(excerpt);
                StatusSubtext.Text = strings.MobileTextCopiedSuccess;
            }
            else if (payload.Files.Count > 0)
            {
                var first = payload.Files[0].FileName;
                var count = payload.Files.Count;
                StatusText.Text = count > 1
                    ? strings.MobileFilesReceived(count, first)
                    : strings.MobileSingleFileReceived(first);
                StatusSubtext.Text = strings.MobileFilesSavedSuccess;
            }
        });
    }

    private void OnCopyUrlClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(_transferUrl);
            SubtleToastWindow.ShowToast(LocalizationManager.Strings.ToastLinkCopied);
        }
        catch
        {
        }
    }

    private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_server.IncomingFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = _server.IncomingFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(LocalizationManager.Strings.MobileFolderOpenError(ex.Message), "WindowsCM", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static BitmapSource RenderQrCode(string payload)
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
