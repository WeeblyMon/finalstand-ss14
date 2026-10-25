using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent]
public sealed partial class FSAilmentComponent : Component
{
    [DataField(required: true)]
    public FSAilmentSlot Slot;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Examine;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);

    [DataField]
    public FSAilmentTier Tier;

    [DataField]
    public bool BlocksRegen;

    [DataField]
    public float IncomingHealScale = 1f;

    [DataField]
    public EntProtoId? BecomesOnExpire;
}

[RegisterComponent]
public sealed partial class FSAilmentWardComponent : Component
{
    [DataField(required: true)]
    public FSAilmentSlot Blocks;
}
