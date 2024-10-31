using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;

namespace BlindCatCore.Core;

[DebuggerDisplay("Count = {Count}")]
public class ConcurrentQueueExt<T>
{
    private readonly LinkedList<T> _items = new LinkedList<T>();
    private readonly object _syncLock = new object();

    public bool IsEmpty
    {
        get
        {
            lock (_syncLock)
            {
                return _items.Count == 0;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _items.Count;
            }
        }
    }

    // ƒобавл€ет элемент в конец очереди
    public void Enqueue(T item)
    {
        lock (_syncLock)
        {
            _items.AddLast(item);
        }
    }

    /// <summary>
    /// ѕытаетс€ извлечь элемент из начала очереди
    /// </summary>
    public bool TryDequeue([MaybeNullWhen(false)] out T result)
    {
        lock (_syncLock)
        {
            if (_items.Count == 0)
            {
                result = default;
                return false;
            }

            result = _items.First.Value;
            _items.RemoveFirst();
            return true;
        }
    }

    /// <summary>
    /// ѕытаетс€ получить элемент из начала очереди, не удал€€ его
    /// </summary>
    public bool TryPeek([MaybeNullWhen(false)] out T result)
    {
        lock (_syncLock)
        {
            if (_items.Count == 0)
            {
                result = default;
                return false;
            }

            result = _items.First.Value;
            return true;
        }
    }

    /// <summary>
    /// ѕытаетс€ получить последний элемент очереди, не удал€€ его
    /// </summary>
    public bool TryLast([MaybeNullWhen(false)] out T result)
    {
        lock (_syncLock)
        {
            if (_items.Count == 0)
            {
                result = default;
                return false;
            }

            result = _items.Last.Value;
            return true;
        }
    }
}