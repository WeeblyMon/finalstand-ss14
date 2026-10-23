using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Sprint;
using Content.Shared.Clothing;
using Content.Shared.Damage.Systems;
using Content.Shared.Ensnaring;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Systems;
using Content.Shared.Wieldable;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSPackMuleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPackMuleComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed,
            after:
            [
                typeof(InventorySystem), typeof(SharedHandsSystem), typeof(StatusEffectsSystem),
                typeof(ClothingSpeedModifierSystem), typeof(HeldSpeedModifierSystem), typeof(SlowOnDamageSystem),
                typeof(SharedEnsnareableSystem), typeof(PullingSystem), typeof(MovementModStatusSystem),
                typeof(SpeedModifierContactsSystem), typeof(SharedStunSystem), typeof(SharedTemperatureSystem),
                typeof(SharedWieldableSystem),
            ],
            before:
            [
                typeof(SharedFSSprintSystem), typeof(FSPerkBuffSystem), typeof(FSSpeedBonusSystem),
                typeof(FSMedicalBonusSystem), typeof(HeldSpeedBonusSystem), typeof(FSCargonianSystem),
            ]);
    }

    private void OnRefreshSpeed(EntityUid uid, FSPackMuleComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (comp.Level <= 0)
            return;

        var easing = FSPerkBonusConstants.PackMulePerLevel * Math.Min(comp.Level, FSPerkDef.MaxLevel);
        args.ModifySpeed(Eased(args.WalkSpeedModifier, easing), Eased(args.SprintSpeedModifier, easing));
    }

    private static float Eased(float modifier, float easing)
    {
        if (modifier >= 1f || modifier <= 0f)
            return 1f;

        return (1f - (1f - modifier) * (1f - easing)) / modifier;
    }
}
