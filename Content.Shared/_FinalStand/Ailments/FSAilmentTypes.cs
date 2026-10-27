using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Ailments;

[Serializable, NetSerializable]
public enum FSAilmentSlot : byte
{
    Bleeding,
    Bone,
    Burn,
    Bloater,
    Heart,
}

[Serializable, NetSerializable]
public enum FSAilmentMeter : byte
{
    Bleed,
    Burn,
    Shock,
}

public enum FSAilmentTier : byte
{
    None,
    Minor,
    Major,
}

public enum FSCureKind : byte
{
    HandsOn,
    Chemical,
    Automatic,
    Mass,
    Rider,
    Wash,
}

[Flags, Serializable, NetSerializable]
public enum FSAilmentPips : byte
{
    None = 0,
    Bleeding = 1 << (int) FSAilmentSlot.Bleeding,
    Bone = 1 << (int) FSAilmentSlot.Bone,
    Burn = 1 << (int) FSAilmentSlot.Burn,
    Bloater = 1 << (int) FSAilmentSlot.Bloater,
    Heart = 1 << (int) FSAilmentSlot.Heart,
}

public record struct FSAilmentMeterState(float Value, TimeSpan Updated, bool Draining);

public readonly record struct FSAilmentView(
    FSAilmentSlot Slot,
    LocId Name,
    int Level,
    TimeSpan? End,
    LocId? Effect,
    LocId? FixHint,
    LocId? Examine,
    bool Ward,
    LocId? Suffix = null,
    SpriteSpecifier? FixIcon = null);

[ByRefEvent]
public record struct FSAilmentInflictAttemptEvent(FSAilmentSlot Slot, bool Cancelled = false);

[ByRefEvent]
public readonly record struct FSAilmentInflictedEvent(EntityUid Patient, FSAilmentSlot Slot, EntityUid? Source, EntProtoId? Effect = null);

[ByRefEvent]
public readonly record struct FSAilmentCuredEvent(EntityUid Patient, FSAilmentSlot Slot, FSAilmentTier Tier, EntityUid? Curer, FSCureKind Kind);

[ByRefEvent]
public record struct FSIncomingHealScaleEvent(float Scale);
