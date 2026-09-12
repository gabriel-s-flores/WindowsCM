// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Forms;
using System.Windows.Threading;
using WindowsCM.Core.Feedback;
using WindowsCM.Core.Tray;

namespace WindowsCM.App;

// Production tray home over WinForms NotifyIcon (research 03 chose
// H.NotifyIcon.WPF, but 2.4.1 ships no net8.0 target — net10 + net462
// only — so the in-box icon covers the same contract): left toggles the
// popup, right opens the five-item menu, balloon + icon flash for copy
// feedback. Double-click aliases single: WinForms raises Click before
// DoubleClick, so the single toggle is deferred by one double-click
// interval and cancelled when the double lands — exactly one toggle
// either way.
internal sealed class TrayManager : IDisposable, ICopyNotifier, IIconFlasher
{
    private readonly TrayController _controller;
    private readonly IIncognitoToggle _incognito;
    private readonly Dispatcher _dispatcher;
    private readonly NotifyIcon _icon;
    private ToolStripMenuItem? _incognitoItem;
    private readonly DispatcherTimer _flashTimer;
    private int _flashTicksLeft;
    private bool _disposed;

    public TrayManager(TrayController controller, IIncognitoToggle incognito, Dispatcher dispatcher)
    {
        _controller = controller;
        _incognito = incognito;
        _dispatcher = dispatcher;

        _icon = new NotifyIcon
        {
            Icon = AppIcons.Base,
            Text = "WindowsCM",
            Visible = true,
        };
        _icon.MouseClick += OnMouseClick;
        _icon.DoubleClick += (_, _) => _dispatcher.Invoke(_controller.OnDoubleClick);

        var menu = new ContextMenuStrip();
        foreach (var item in TrayMenu.All)
        {
            if (item == TrayMenuItem.Incognito)
            {
                _incognitoItem = new ToolStripMenuItem(TrayMenu.LabelFor(item))
                {
                    CheckOnClick = true,
                };
                _incognitoItem.Click += (_, _) => _controller.OnMenu(item);
                menu.Items.Add(_incognitoItem);
                continue;
            }
            var captured = item;
            var entry = new ToolStripMenuItem(TrayMenu.LabelFor(item));
            entry.Click += (_, _) => _controller.OnMenu(captured);
            menu.Items.Add(entry);
        }
        menu.Opening += (_, _) =>
        {
            if (_incognitoItem is not null)
            {
                _incognitoItem.Checked = _incognito.IsIncognito;
            }
        };
        _icon.ContextMenuStrip = menu;

        _flashTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(CopyFeedbackService.FlashIntervalMs),
        };
        _flashTimer.Tick += (_, _) => OnFlashTick();
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _dispatcher.Invoke(_controller.OnLeftClick);
        }
    }

    public void ShowBalloon(string title, string text) =>
        _dispatcher.Invoke(() =>
        {
            if (!_disposed)
            {
                _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.None);
            }
        });

    public void Flash(int times, int intervalMs) =>
        _dispatcher.Invoke(() =>
        {
            if (_disposed)
            {
                return;
            }
            _flashTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, intervalMs));
            _flashTicksLeft = Math.Max(1, times) * 2;
            _flashTimer.Start();
        });

    private void OnFlashTick()
    {
        if (_disposed)
        {
            _flashTimer.Stop();
            return;
        }
        _flashTicksLeft--;
        _icon.Icon = _flashTicksLeft % 2 == 0 ? AppIcons.Base : AppIcons.Overlay;
        if (_flashTicksLeft <= 0)
        {
            _flashTimer.Stop();
            _icon.Icon = AppIcons.Base;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _flashTimer.Stop();
        _icon.Visible = false;
        _icon.Dispose();
    }
}
