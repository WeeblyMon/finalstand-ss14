namespace Content.Shared._FinalStand.Effects;

// Rate limit for cosmetic effects (sounds, lunges, hit flashes) that a horde would otherwise send
// to every client hundreds of times a second. One reservation per key per interval.
public sealed class FSEffectThrottle<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, TimeSpan> _next = new();
    private readonly TimeSpan _interval;
    private TimeSpan _nextPrune;

    public FSEffectThrottle(TimeSpan interval)
    {
        _interval = interval;
    }

    public bool TryReserve(TKey key, TimeSpan now)
    {
        if (_next.TryGetValue(key, out var next) && now < next)
            return false;

        if (now >= _nextPrune)
        {
            _nextPrune = now + _interval;
            foreach (var (stale, until) in _next)
            {
                if (until <= now)
                    _next.Remove(stale);
            }
        }

        _next[key] = now + _interval;
        return true;
    }
}

public static class FSEffectArea
{
    public const float CellSize = 2f;

    // Coarse cell an entity stands in, so nearby NPCs share one reservation.
    public static (EntityUid, Vector2i) Key(TransformComponent xform)
    {
        return (xform.ParentUid, (xform.LocalPosition / CellSize).Floored());
    }
}
