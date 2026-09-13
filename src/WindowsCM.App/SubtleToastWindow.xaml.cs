// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace WindowsCM.App;

public partial class SubtleToastWindow : Window
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int GwlExStyle = -20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private static SubtleToastWindow? _currentToast;

    public SubtleToastWindow(string message)
    {
        InitializeComponent();
        ToastText.Text = message;
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, exStyle | WsExNoActivate | WsExToolWindow);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionToast();
        AnimateToast();
    }

    private void PositionToast()
    {
        var pt = new POINT();
        GetCursorPos(out pt);
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y))
                     ?? System.Windows.Forms.Screen.PrimaryScreen;

        if (screen is null)
        {
            Left = 100;
            Top = 100;
            return;
        }

        var src = PresentationSource.FromVisual(this);
        var (dx, dy) = src?.CompositionTarget is not null
            ? (src.CompositionTarget.TransformFromDevice.M11, src.CompositionTarget.TransformFromDevice.M22)
            : (1.0, 1.0);

        var workArea = screen.WorkingArea;
        var workWidthDips = workArea.Width * dx;
        var workHeightDips = workArea.Height * dy;
        var workLeftDips = workArea.Left * dx;
        var workTopDips = workArea.Top * dy;

        UpdateLayout();
        var toastWidth = ActualWidth > 0 ? ActualWidth : 260;
        var toastHeight = ActualHeight > 0 ? ActualHeight : 45;

        Left = workLeftDips + (workWidthDips - toastWidth) / 2;
        Top = workTopDips + workHeightDips - toastHeight - 55;
    }

    private void AnimateToast()
    {
        Opacity = 0;
        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1550))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1850))));

        animation.Completed += (_, _) =>
        {
            if (_currentToast == this)
            {
                _currentToast = null;
            }
            Close();
        };

        BeginAnimation(OpacityProperty, animation);
    }

    public static void ShowToast(string message)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            ShowToastInternal(message);
        }
        else
        {
            dispatcher.BeginInvoke(() => ShowToastInternal(message));
        }
    }

    private static void ShowToastInternal(string message)
    {
        try
        {
            _currentToast?.Close();
        }
        catch
        {
        }

        var toast = new SubtleToastWindow(message);
        _currentToast = toast;
        toast.Show();
    }
}
