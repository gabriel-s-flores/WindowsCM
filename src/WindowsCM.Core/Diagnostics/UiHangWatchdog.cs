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
    private readonly Func<string>? _describeActivity;
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
        Func<string>? describeProcess = null,
        Func<string>? describeActivity = null,
        TimeSpan? samplePeriod = null)
    {
        _postToUi = postToUi;
        _report = report;
        Threshold = threshold;
        Interval = interval ?? TimeSpan.FromSeconds(1);
        SamplePeriod = samplePeriod ?? TimeSpan.FromMilliseconds(250);
        _describeProcess = describeProcess;
        _describeActivity = describeActivity;
    }

    public TimeSpan Threshold { get; }

    public TimeSpan Interval { get; }

    // How often the stage of a stuck UI is sampled until it answers: a hang
    // can move through several stages (a layout pass, then a re-theme).
    public TimeSpan SamplePeriod { get; }

    private const int MaxStagesReported = 8;

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
                var stage = DescribeActivity();
                _report(string.Format(CultureInfo.InvariantCulture,
                    "the UI thread has not answered for {0:0.#} s{1}{2}",
                    Threshold.TotalSeconds, Describe(),
                    stage is null ? "" : "; stage: " + stage));
                // Distinct stages seen while stuck, with when each was first
                // seen (seconds since the ping was sent).
                var stages = new List<string>();
                if (stage is not null)
                {
                    stages.Add(string.Format(CultureInfo.InvariantCulture, "at {0:0.0} s: {1}",
                        Stopwatch.GetElapsedTime(sentAt).TotalSeconds, stage));
                }
                var lastStage = stage;
                while (!WaitAnsweredOrStopped(answered, SamplePeriod))
                {
                    if (_stop.IsSet)
                    {
                        return;
                    }
                    var now = DescribeActivity();
                    if (now is not null && StageKey(now) != StageKey(lastStage) && stages.Count < MaxStagesReported)
                    {
                        stages.Add(string.Format(CultureInfo.InvariantCulture, "at {0:0.0} s: {1}",
                            Stopwatch.GetElapsedTime(sentAt).TotalSeconds, now));
                    }
                    lastStage = now;
                }
                if (_stop.IsSet)
                {
                    return;
                }
                _report(string.Format(CultureInfo.InvariantCulture,
                    "the UI thread answered again after {0:0.0} s{1}",
                    Stopwatch.GetElapsedTime(sentAt).TotalSeconds,
                    stages.Count == 0 ? "" : "; stages seen: " + string.Join(" | ", stages)));
            }
            _stop.Wait(Interval);
        }
    }

    private bool WaitAnsweredOrStopped(ManualResetEventSlim answered, TimeSpan timeout)
    {
        WaitHandle.WaitAny([answered.WaitHandle, _stop.WaitHandle], timeout);
        return answered.IsSet;
    }

    private string? DescribeActivity()
    {
        try
        {
            var text = _describeActivity?.Invoke();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    // A stage without its running durations ("hotkey 2.1 s > show 0.4 s"
    // and "hotkey 2.4 s > show 0.7 s" are the same stage).
    private static string? StageKey(string? stage) =>
        stage is null ? null : System.Text.RegularExpressions.Regex.Replace(stage, @" \d+(\.\d+)? s", "");

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
