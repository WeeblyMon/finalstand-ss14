using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Repair;

// Repairs the local player made in the last half second on one structure, summed so a rivet stream reads as one number.
[Serializable, NetSerializable]
public sealed class FSRepairNumberEvent(NetEntity target, float amount) : EntityEventArgs
{
    public readonly NetEntity Target = target;
    public readonly float Amount = amount;
}

// A rivet landed on a structure already repaired up to the gun's cap.
[Serializable, NetSerializable]
public sealed class FSRivetCapEvent(NetEntity target) : EntityEventArgs
{
    public readonly NetEntity Target = target;
}
