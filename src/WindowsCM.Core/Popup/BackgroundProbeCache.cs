// SPDX-License-Identifier: GPL-3.0-or-later
using WindowsCM.Core.Diagnostics;

namespace WindowsCM.Core.Popup;

// Answers that are slow to get (a file's size, thumbnail or tags), read by
// the UI without ever waiting for them. A card used to ask the disk and the
// Shell directly while it was being realized: on a share that is offline
// (or WSL, a sleeping drive, a removed USB stick) every call blocked the UI
// thread for seconds, so scrolling to that card froze the popup.
//
// TryGet never runs the probe on the caller's thread. A miss, or an entry
// older than freshFor, schedules one probe per key (concurrent lookups
// share it); a stale value is still returned meanwhile so the card does
// not flicker. Updated fires, on the scheduler's thread, only when a probe
// stored a different answer — the UI refreshes then.
public sealed class BackgroundProbeCache<TValue>
{
    private readonly Func<string, TValue> _probe;
    private readonly Action<Action> _schedule;
    private readonly TimeSpan _freshFor;
    private readonly Func<DateTime> _utcNow;
    private readonly Action<string, Exception>? _onProbeFailed;
    private readonly LruCache<string, (TValue Value, DateTime ProbedAt)> _entries;
    private readonly HashSet<string> _pending;
    private readonly object _gate = new();

    public BackgroundProbeCache(
        Func<string, TValue> probe,
        Action<Action> schedule,
        TimeSpan freshFor,
        int capacity,
        Func<DateTime>? utcNow = null,
        IEqualityComparer<string>? comparer = null,
        Action<string, Exception>? onProbeFailed = null)
    {
        _probe = probe;
        _schedule = schedule;
        _freshFor = freshFor;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _onProbeFailed = onProbeFailed;
        _entries = new LruCache<string, (TValue, DateTime)>(capacity, comparer);
        _pending = new HashSet<string>(comparer);
    }

    public event Action<string>? Updated;

    public bool TryGet(string key, out TValue value)
    {
        var found = _entries.TryGet(key, out var entry);
        value = found ? entry.Value : default!;
        if (!found || _utcNow() - entry.ProbedAt > _freshFor)
        {
            Schedule(key);
        }
        return found;
    }

    public void Clear() => _entries.Clear();

    private void Schedule(string key)
    {
        lock (_gate)
        {
            if (!_pending.Add(key))
            {
                return;
            }
        }
        _schedule(() => Probe(key));
    }

    private void Probe(string key)
    {
        try
        {
            var value = _probe(key);
            var changed = !_entries.TryGet(key, out var previous)
                || !EqualityComparer<TValue>.Default.Equals(previous.Value, value);
            _entries.Set(key, (value, _utcNow()));
            if (changed)
            {
                Updated?.Invoke(key);
            }
        }
        catch (Exception ex)
        {
            _onProbeFailed?.Invoke(key, ex);
        }
        finally
        {
            lock (_gate)
            {
                _pending.Remove(key);
            }
        }
    }
}
