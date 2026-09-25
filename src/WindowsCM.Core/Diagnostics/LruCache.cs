// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Diagnostics;

// Thread-safe, size-bounded cache with least-recently-used eviction. The
// preview caches (thumbnails, media metadata) used unbounded dictionaries
// keyed by path + write time, so a long session over many files only ever
// grew. Null values are cached too ("no thumbnail" is a real answer).
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map;
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new();
    private readonly object _gate = new();

    public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        Capacity = Math.Max(1, capacity);
        _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(comparer);
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }
            var node = _order.AddFirst((key, value));
            _map[key] = node;
            while (_map.Count > Capacity && _order.Last is { } oldest)
            {
                _order.RemoveLast();
                _map.Remove(oldest.Value.Key);
            }
        }
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (TryGet(key, out var cached))
        {
            return cached;
        }
        // The factory runs outside the lock (it may decode files or call
        // the shell); a racing duplicate just overwrites with an equal value.
        var created = factory(key);
        Set(key, created);
        return created;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _order.Clear();
        }
    }
}
