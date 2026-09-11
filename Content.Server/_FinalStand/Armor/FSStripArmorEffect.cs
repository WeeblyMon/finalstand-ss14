using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.EntityEffects;

namespace Content.Server._FinalStand.Armor;

public sealed partial class FSStripArmorSystem : EntityEffectSystem<FSArmorComponent, FSStripArmor>
{
    protected override void Effect(Entity<FSArmorComponent> entity, ref EntityEffectEvent<FSStripArmor> args)
    {
        var armor = entity.Comp;
        if (armor.MaxArmor <= 0f)
            return;

        var removed = armor.MaxArmor * Math.Clamp(args.Effect.Fraction, 0f, 1f);

        armor.MaxArmor = MathF.Max(0f, armor.MaxArmor - removed);
        armor.CurrentArmor = MathF.Min(armor.CurrentArmor, armor.MaxArmor);
        armor.LastSyncedArmor = armor.CurrentArmor;

        Dirty(entity.Owner, armor);
    }
}
