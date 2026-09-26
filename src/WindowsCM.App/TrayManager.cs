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
// feedback. Double-click aliases single: WinForms raises MouseClick on the
// first release and DoubleClick (not a second MouseClick) on the second
// press, so only MouseClick toggles — exactly one toggle either way. Also
// toggling on DoubleClick opened the popup and closed it again at once.
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

    private readonly Dictionary<TrayMenuItem, ToolStripMenuItem> _menuItems = new();
    private ToolStripMenuItem? _compactMenuItem;
    private ToolStripMenuItem? _autoPasteItem;
    private readonly Func<bool>? _isAutoPaste;

    public TrayManager(TrayController controller, IIncognitoToggle incognito, Dispatcher dispatcher,
        Action? onOpenCompact = null, Func<bool>? isAutoPaste = null, Action<bool>? setAutoPaste = null)
    {
        _controller = controller;
        _incognito = incognito;
        _dispatcher = dispatcher;
        _isAutoPaste = isAutoPaste;

        _icon = new NotifyIcon
        {
            Icon = AppIcons.Base,
            Text = "WindowsCM",
            Visible = true,
        };
        _icon.MouseClick += OnMouseClick;

        var menu = new ContextMenuStrip();
        foreach (var item in TrayMenu.All)
        {
            var captured = item;
            var entry = new ToolStripMenuItem(TrayMenu.LabelFor(item));
            entry.Click += (_, _) => _controller.OnMenu(captured);
            _menuItems[item] = entry;
            menu.Items.Add(entry);

            if (item == TrayMenuItem.Incognito)
            {
                _incognitoItem = entry;

                // Checkable next to the other mode toggle: picks paste into
                // the previously focused field (default) or only copy.
                if (isAutoPaste is not null && setAutoPaste is not null)
                {
                    _autoPasteItem = new ToolStripMenuItem(WindowsCM.Core.Localization.LocalizationManager.Strings.TrayAutoPaste)
                    {
                        CheckOnClick = false,
                        Checked = isAutoPaste(),
                    };
                    _autoPasteItem.Click += (_, _) => _dispatcher.Invoke(() => setAutoPaste(!isAutoPaste()));
                    menu.Items.Add(_autoPasteItem);
                }
            }

            if (item == TrayMenuItem.Open && onOpenCompact is not null)
            {
                _compactMenuItem = new ToolStripMenuItem(WindowsCM.Core.Localization.LocalizationManager.Strings.TrayCompactMenu);
                _compactMenuItem.Click += (_, _) => _dispatcher.Invoke(onOpenCompact);
                menu.Items.Add(_compactMenuItem);
            }
        }
        menu.Opening += (_, _) =>
        {
            if (_autoPasteItem is not null && _isAutoPaste is not null)
            {
                _autoPasteItem.Checked = _isAutoPaste();
            }
            if (_incognitoItem is not null)
            {
                _incognitoItem.Checked = _incognito.IsIncognito;
                _incognitoItem.Text = _incognito.IsIncognito
                    ? WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIncognitoActive
                    : WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIncognito;
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

    // Copy feedback arrives on the clipboard listener thread: queued, never
    // waited for, so a busy UI thread does not hold up the next capture.
    public void ShowBalloon(string title, string text) =>
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                _icon.ShowBalloonTip(3000, title, text, ToolTipIcon.None);
            }
        });

    public void Flash(int times, int intervalMs) =>
        _dispatcher.BeginInvoke(() =>
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

    public void UpdateIncognitoState(bool isIncognito)
    {
        _dispatcher.Invoke(() =>
        {
            if (_incognitoItem is not null)
            {
                _incognitoItem.Checked = isIncognito;
                _incognitoItem.Text = isIncognito
                    ? WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIncognitoActive
                    : WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIncognito;
            }
            if (_icon is not null)
            {
                _icon.Text = isIncognito
                    ? WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIconTooltipIncognito
                    : WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIconTooltip;
            }
        });
    }

    public void UpdateLanguage()
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var (key, item) in _menuItems)
            {
                item.Text = TrayMenu.LabelFor(key);
            }
            if (_compactMenuItem is not null)
            {
                _compactMenuItem.Text = WindowsCM.Core.Localization.LocalizationManager.Strings.TrayCompactMenu;
            }
            if (_autoPasteItem is not null)
            {
                _autoPasteItem.Text = WindowsCM.Core.Localization.LocalizationManager.Strings.TrayAutoPaste;
            }
            if (_incognitoItem is not null)
            {
                _incognitoItem.Text = _incognito.IsIncognito
                    ? WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIncognitoActive
                    : WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIncognito;
            }
            if (_icon is not null)
            {
                _icon.Text = _incognito.IsIncognito
                    ? WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIconTooltipIncognito
                    : WindowsCM.Core.Localization.LocalizationManager.Strings.TrayIconTooltip;
            }
        });
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
