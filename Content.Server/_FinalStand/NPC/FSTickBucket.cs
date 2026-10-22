namespace Content.Server._FinalStand.NPC;

// Spreads per-zombie work over ticks. A wave zombie's uid is the last one's plus a fixed stride (it spawns child
// entities of its own), so uid % n can stack a whole horde into one slot. Multiplying by an odd constant spreads any
// stride evenly over the high bits, and the slot is taken from those.
public static class FSTickBucket
{
    public static uint Hash(EntityUid uid)
    {
        return (uint) uid.Id * 2654435761u;
    }

    public static int Of(EntityUid uid, int buckets)
    {
        return (int) ((ulong) Hash(uid) * (uint) buckets >> 32);
    }
}
