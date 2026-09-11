// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;

namespace WindowsCM.App;

// QR payload dialog (ticket 13 hands the eligibility + payload to the UI;
// Core never renders bitmaps). QRCoder draws, WPF shows.
public partial class QrWindow : Window
{
    public QrWindow(string payload)
    {
        InitializeComponent();
        PayloadText.Text = payload;
        QrImage.Source = Render(payload);
    }

    private static BitmapSource Render(string payload)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
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
