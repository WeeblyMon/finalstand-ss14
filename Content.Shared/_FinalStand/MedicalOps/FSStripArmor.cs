using Content.Shared.EntityEffects;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSStripArmor : EntityEffectBase<FSStripArmor>
{
    [DataField]
    public float Fraction = 1f;
}
