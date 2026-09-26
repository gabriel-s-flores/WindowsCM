// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace WindowsCM.Core.Capture.Win32;

// Message-only window listener for WM_CLIPBOARDUPDATE (research 02: posted
// directly, so message-only windows work; no broadcast needed). Runs its own
// STA message pump; the monitor subscribes and never polls.
public sealed class MessageOnlyClipboardListener : IClipboardChangeSource, IDisposable
{
    private readonly Thread _thread;
    private IntPtr _hwnd;
    private NativeClipboard.WndProc? _wndProc;
    private readonly ManualResetEventSlim _ready = new(false);
    // Reads wait for the source app to finish its copy (see
    // ClipboardUpdateCoalescer). The timer is a window timer, so the
    // delayed read still runs on this STA thread, as the reader requires.
    private readonly ClipboardUpdateCoalescer _coalescer = new();
    private static readonly UIntPtr ReadTimerId = new(1);
    private Exception? _startupError;
    private bool _disposed;

    public event EventHandler? ClipboardChanged;

    // Handlers run inside this window procedure, called from native code:
    // an exception escaping it terminates the process, so every failure is
    // caught and reported here instead (the app logs it).
    public event EventHandler<Exception>? HandlerFailed;

    public MessageOnlyClipboardListener()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Win32 clipboard requires Windows.");
        }
        _thread = new Thread(MessageLoop) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            throw _startupError;
        }
    }

    private void MessageLoop()
    {
        try
        {
            _wndProc = WndProc;
            var hInstance = NativeClipboard.GetModuleHandle(null);
            var className = "WindowsCMClipboardListener-" + Guid.NewGuid();
            var wc = new NativeClipboard.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeClipboard.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = hInstance,
                lpszClassName = className,
            };
            if (NativeClipboard.RegisterClassExW(ref wc) == 0)
            {
                throw new InvalidOperationException("RegisterClassExW failed.");
            }
            _hwnd = NativeClipboard.CreateWindowExW(
                0, className, "", 0, 0, 0, 0, 0,
                NativeClipboard.HWND_MESSAGE, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateWindowExW failed.");
            }
            if (!NativeClipboard.AddClipboardFormatListener(_hwnd))
            {
                throw new InvalidOperationException("AddClipboardFormatListener failed.");
            }
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
            return;
        }
        _ready.Set();

        while (NativeClipboard.GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            NativeClipboard.TranslateMessage(ref msg);
            NativeClipboard.DispatchMessage(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeClipboard.WM_CLIPBOARDUPDATE)
        {
            var delay = _coalescer.OnUpdate(Environment.TickCount64);
            // Re-arming the same timer id replaces the pending one: a burst
            // ends in a single read of the final content.
            NativeClipboard.SetTimer(hWnd, ReadTimerId, (uint)Math.Max(1, delay), IntPtr.Zero);
            return IntPtr.Zero;
        }
        if (msg == NativeClipboard.WM_TIMER && wParam == (IntPtr)(long)ReadTimerId.ToUInt64())
        {
            NativeClipboard.KillTimer(hWnd, ReadTimerId);
            _coalescer.OnRead();
            try
            {
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
            }
            return IntPtr.Zero;
        }
        if (msg == NativeClipboard.WM_CLOSE)
        {
            // Posted by Dispose: the window must be torn down (and the loop
            // quit) on the thread that owns it.
            NativeClipboard.RemoveClipboardFormatListener(hWnd);
            NativeClipboard.DestroyWindow(hWnd);
            NativeClipboard.PostQuitMessage(0);
            return IntPtr.Zero;
        }
        return NativeClipboard.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void ReportFailure(Exception ex)
    {
        try
        {
            HandlerFailed?.Invoke(this, ex);
        }
        catch
        {
            // Reporting is best effort; nothing may escape to native code.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        // DestroyWindow and PostQuitMessage only act on the calling thread's
        // own windows/queue, so calling them here (the UI thread) left the
        // listener pumping and made every exit wait out the Join timeout.
        // Ask the listener thread to shut itself down instead.
        if (_hwnd != IntPtr.Zero)
        {
            NativeClipboard.PostMessageW(_hwnd, NativeClipboard.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            _hwnd = IntPtr.Zero;
        }
        // Let the STA pump drain; never block the caller forever.
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }
}
