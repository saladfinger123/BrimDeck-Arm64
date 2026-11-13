namespace BrimDeck.Core;

// Whether two samples are close enough to call the difference a current rate. The monitor interval is one or two
// seconds, so a few seconds of stall still counts. A sleep or a pause longer than the maximum is not averaged into
// "the speed right now": the caller keeps the new counters as the next baseline and reports the sample unavailable.
public static class SampleFreshness
{
    public const double NetworkMaximumGapSeconds = 5;

    public static bool Accept(double previousAt, double now, double maximumGapSeconds) =>
        maximumGapSeconds > 0 && now > previousAt && now - previousAt <= maximumGapSeconds;
}

// Bytes per second across physical adapters. A counter that went backwards (sleep, reset, an adapter that came back)
// makes the whole sample unavailable. It is not reported as zero, and the other adapters' bytes are not shown as if
// the reset one had been idle.
public static class NetworkDelta
{
    public static double? Sum(IReadOnlyList<(ulong Previous, ulong Current)> counters, double seconds)
    {
        if (seconds <= 0 || counters.Count == 0) return null;
        double total = 0;
        foreach (var (previous, current) in counters)
        {
            if (MonitorMetrics.Rate(previous, current, seconds) is not { } rate) return null;
            total += rate;
        }
        return total;
    }
}
