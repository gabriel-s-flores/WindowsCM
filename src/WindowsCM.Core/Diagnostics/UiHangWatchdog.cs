// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;

namespace WindowsCM.Core.Diagnostics;

// Notices when the UI thread stops answering and reports it (to the error
// log): once when it has been stuck for Threshold, and again with the total
// when it answers. A frozen popup used to leave no trace — Windows showed
// "Not responding" and, if the user closed it, the log stayed empty.
//
// A background thread posts a no-op to the UI every Interval; a no-op that
// has not run after Threshold means the UI thread is blocked inside one
// piece of work (a file on a dead share, a layout loop, ...).
public sealed class UiHangWatchdog : IDisposable
{
    private readonly Action<Action> _postToUi;
    private readonly Action<string> _report;
    private readonly Func<string>? _describeProcess;
    private readonly ManualResetEventSlim _stop = new();
    // One event reused for every ping. A fresh one per ping (every second)
    // left a kernel handle for the finalizer each time: thousands per hour
    // on an idle tray app, where collections are rare. Never disposed, so a
    // ping still queued at shutdown can still Set() it.
    private readonly ManualResetEventSlim _answered = new();
    private Thread? _thread;

    public UiHangWatchdog(
        Action<Action> postToUi,
        Action<string> report,
        TimeSpan threshold,
        TimeSpan? interval = null,
        Func<string>? describeProcess = null)
    {
        _postToUi = postToUi;
        _report = report;
        Threshold = threshold;
        Interval = interval ?? TimeSpan.FromSeconds(1);
        _describeProcess = describeProcess;
    }

    public TimeSpan Threshold { get; }

    public TimeSpan Interval { get; }

    public void Start()
    {
        _thread ??= new Thread(Watch) { IsBackground = true, Name = "WindowsCM UI watchdog" };
        _thread.Start();
    }

    public void Dispose() => _stop.Set();

    private void Watch()
    {
        while (!_stop.IsSet)
        {
            // The previous ping has always run by now: a late one is waited
            // for (below) before the next is sent.
            var answered = _answered;
            answered.Reset();
            var sentAt = Stopwatch.GetTimestamp();
            try
            {
                _postToUi(answered.Set);
            }
            catch
            {
                // The dispatcher is shutting down: nothing left to watch.
                return;
            }
            if (!WaitAnsweredOrStopped(answered, Threshold))
            {
                if (_stop.IsSet)
                {
                    return;
                }
                _report(string.Format(CultureInfo.InvariantCulture,
                    "the UI thread has not answered for {0:0.#} s{1}",
                    Threshold.TotalSeconds, Describe()));
                WaitAnsweredOrStopped(answered, Timeout.InfiniteTimeSpan);
                if (_stop.IsSet)
                {
                    return;
                }
                _report(string.Format(CultureInfo.InvariantCulture,
                    "the UI thread answered again after {0:0.0} s",
                    Stopwatch.GetElapsedTime(sentAt).TotalSeconds));
            }
            _stop.Wait(Interval);
        }
    }

    private bool WaitAnsweredOrStopped(ManualResetEventSlim answered, TimeSpan timeout)
    {
        WaitHandle.WaitAny([answered.WaitHandle, _stop.WaitHandle], timeout);
        return answered.IsSet;
    }

    private string Describe()
    {
        try
        {
            var text = _describeProcess?.Invoke();
            return string.IsNullOrWhiteSpace(text) ? "" : $" ({text})";
        }
        catch
        {
            return "";
        }
    }
}
