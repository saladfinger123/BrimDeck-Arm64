namespace BrimDeck.Core;

// One demand lifetime for samples that outlive the call that started them. Stopping and starting each move the
// epoch, so a snapshot collected across that boundary is not delivered into the new run. The worker generation is
// separate: replacing a stuck thread does not by itself start a new demand.
public sealed class SnapshotPump<T>
{
    private int _epoch;
    private readonly LatestWinsMailbox<Stamped<T>> _mailbox = new();

    public int Epoch => Volatile.Read(ref _epoch);

    public void EndDemand()
    {
        Interlocked.Increment(ref _epoch);
        _mailbox.Invalidate();
    }

    public void BeginDemand() => Interlocked.Increment(ref _epoch);

    // epoch and generation were captured before the slow read. currentGeneration is read after it.
    // A mismatch does not enter the mailbox, so a later delivery cannot apply that snapshot.
    public bool TrySubmit(T value, int epoch, int generation, int currentGeneration)
    {
        if (!MayApply(epoch, Volatile.Read(ref _epoch), generation, currentGeneration, wanted: true)) return false;
        return _mailbox.Submit(new Stamped<T>(value, epoch, generation));
    }

    public bool TryTake(out Stamped<T> value) => _mailbox.TryTake(out value);

    public static bool MayApply(int capturedEpoch, int currentEpoch, int capturedGeneration, int currentGeneration, bool wanted) =>
        wanted && capturedEpoch == currentEpoch && capturedGeneration == currentGeneration;
}

public readonly record struct Stamped<T>(T Value, int Epoch, int Generation);
