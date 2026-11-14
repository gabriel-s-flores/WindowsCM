// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WindowsCM.Core.Diagnostics;

// What the UI thread is doing right now, readable from another thread: the
// hang watchdog names the stage a frozen UI is stuck in. The log used to
// say only that the UI hung (with process counters), never where.
//
// The UI thread pushes named stages (hotkey, model refresh, Show, ...) and,
// through the dispatcher hooks, the operation being run; the watchdog
// thread reads a snapshot. Stages are code labels, never clipboard content.
// Frames are immutable and published through one volatile field, so a
// reader always sees a consistent chain without locking the UI thread.
public sealed class UiActivity
{
    public static UiActivity Shared { get; } = new();

    private readonly Func<long> _timestamp;
    private volatile Frame? _current;
    private volatile Finished? _lastFinished;

    public UiActivity(Func<long>? timestamp = null)
    {
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
    }

    // Names a queued piece of work (a dispatcher operation) lazily, on the
    // reading thread, only when a hang is being described.
    public Func<object, string>? DescribeWork { get; set; }

    internal sealed record Frame(string? Name, object? Work, long StartedAt, Frame? Parent);

    private sealed record Finished(string Name, long EndedAt);

    // Enter on the UI thread; dispose on the same thread, in LIFO order.
    public Scope Enter(string stage) => new(this, Push(new Frame(stage, null, _timestamp(), _current)));

    // For dispatcher hooks: the work object is described only if needed.
    public void EnterWork(object work) => Push(new Frame(null, work, _timestamp(), _current));

    public void ExitWork(object work)
    {
        for (var frame = _current; frame is not null; frame = frame.Parent)
        {
            if (ReferenceEquals(frame.Work, work))
            {
                Pop(frame);
                return;
            }
        }
    }

    private Frame Push(Frame frame)
    {
        _current = frame;
        return frame;
    }

    // Unwinds to the frame's parent: a stage left without its inner frames
    // being closed (an exception through a nested dispatcher loop) never
    // leaves stale frames on top.
    private void Pop(Frame frame)
    {
        for (var probe = _current; probe is not null; probe = probe.Parent)
        {
            if (ReferenceEquals(probe, frame))
            {
                _current = frame.Parent;
                if (frame.Name is not null)
                {
                    _lastFinished = new Finished(frame.Name, _timestamp());
                }
                return;
            }
        }
    }

    // "hotkey 2.3 s > compact.show 2.3 s > Render ContextLayoutManager.UpdateLayoutCallback 2.1 s",
    // outermost first; or what finished last when nothing is tracked.
    public string Describe()
    {
        var now = _timestamp();
        var frames = new List<Frame>();
        for (var frame = _current; frame is not null && frames.Count < 16; frame = frame.Parent)
        {
            frames.Add(frame);
        }
        if (frames.Count == 0)
        {
            var last = _lastFinished;
            return last is null
                ? "no tracked stage (message loop or a window procedure)"
                : string.Format(CultureInfo.InvariantCulture,
                    "no tracked stage (message loop or a window procedure); last finished: {0}, {1:0.0} s ago",
                    last.Name, Seconds(now - last.EndedAt));
        }
        var builder = new StringBuilder();
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            var frame = frames[i];
            if (builder.Length > 0)
            {
                builder.Append(" > ");
            }
            builder.Append(NameOf(frame)).Append(' ')
                .Append(Seconds(now - frame.StartedAt).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s");
        }
        return builder.ToString();
    }

    private string NameOf(Frame frame)
    {
        if (frame.Name is not null)
        {
            return frame.Name;
        }
        try
        {
            return DescribeWork?.Invoke(frame.Work!) ?? "dispatcher operation";
        }
        catch
        {
            return "dispatcher operation";
        }
    }

    private static double Seconds(long ticks) => Math.Max(0, ticks) / (double)Stopwatch.Frequency;

    public readonly struct Scope : IDisposable
    {
        private readonly UiActivity? _owner;
        private readonly Frame? _frame;

        internal Scope(UiActivity owner, Frame frame)
        {
            _owner = owner;
            _frame = frame;
        }

        public void Dispose()
        {
            if (_owner is not null && _frame is not null)
            {
                _owner.Pop(_frame);
            }
        }
    }
}

// Durations of the named steps of one sequence (startup, the first popup
// open), formatted as a single log line. Steps are code labels only.
public sealed class PhaseTimeline
{
    private readonly Func<long> _timestamp;
    private readonly List<(string Name, double Ms)> _phases = [];
    private readonly long _startedAt;

    public PhaseTimeline(Func<long>? timestamp = null)
    {
        _timestamp = timestamp ?? Stopwatch.GetTimestamp;
        _startedAt = _timestamp();
    }

    public double TotalMilliseconds => Milliseconds(_timestamp() - _startedAt);

    public IReadOnlyList<(string Name, double Ms)> Phases => _phases;

    // Times the step until disposed; also marks it as the UI stage, so a
    // hang inside it is named by the watchdog.
    public Step Measure(string name, UiActivity? activity = null) =>
        new(this, name, _timestamp(), activity?.Enter(name));

    public void Add(string name, double milliseconds) => _phases.Add((name, milliseconds));

    public string Format()
    {
        var builder = new StringBuilder();
        foreach (var (name, ms) in _phases)
        {
            builder.Append(name).Append(' ').Append(ms.ToString("0", CultureInfo.InvariantCulture)).Append(" ms, ");
        }
        builder.Append("total ").Append(TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)).Append(" ms");
        return builder.ToString();
    }

    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    public readonly struct Step : IDisposable
    {
        private readonly PhaseTimeline? _owner;
        private readonly string _name;
        private readonly long _startedAt;
        private readonly UiActivity.Scope? _scope;

        internal Step(PhaseTimeline owner, string name, long startedAt, UiActivity.Scope? scope)
        {
            _owner = owner;
            _name = name;
            _startedAt = startedAt;
            _scope = scope;
        }

        public void Dispose()
        {
            _scope?.Dispose();
            _owner?.Add(_name, Milliseconds(_owner._timestamp() - _startedAt));
        }
    }
}
