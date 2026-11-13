namespace BrimDeck.Core;

// One pending delivery. Posting again before the UI takes the value replaces it, so a slow dispatcher applies the
// newest sample and does not replay the ones that queued behind it. Invalidate drops whatever is waiting; a take
// after that returns nothing, which is how a fast stop avoids painting a sample that belongs to the previous run.
public sealed class LatestWinsMailbox<T>
{
    private readonly object _gate = new();
    private T? _pending;
    private bool _has;
    private bool _scheduled;

    // True when the caller should schedule the single delivery. False when one is already scheduled; it will
    // observe the value stored by this call.
    public bool Submit(T value)
    {
        lock (_gate)
        {
            _pending = value;
            _has = true;
            if (_scheduled) return false;
            _scheduled = true;
            return true;
        }
    }

    public bool TryTake(out T value)
    {
        lock (_gate)
        {
            _scheduled = false;
            if (!_has)
            {
                value = default!;
                return false;
            }
            value = _pending!;
            _pending = default;
            _has = false;
            return true;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _pending = default;
            _has = false;
        }
    }
}
