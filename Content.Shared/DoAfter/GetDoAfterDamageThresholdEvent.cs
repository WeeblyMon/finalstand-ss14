namespace Content.Shared.DoAfter;

/// <summary>
/// Raised on an entity taking damage mid-do-after to collect extra damage it can absorb before the
/// do-after is cancelled.
/// </summary>
[ByRefEvent]
public struct GetDoAfterDamageThresholdEvent
{
    public float Extra;
}
