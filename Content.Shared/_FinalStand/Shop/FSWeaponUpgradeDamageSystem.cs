using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared._FinalStand.Shop;

public sealed partial class FSWeaponUpgradeDamageSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, FSHitscanDamageModifyEvent>(OnHitscanDamage);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, DamageExamineEvent>(OnDamageExamine);
    }

    private void OnGetMeleeDamage(EntityUid weapon, FSWeaponUpgradeStateComponent state, ref GetMeleeDamageEvent args)
    {
        if (state.DamageMultiplier > 1f)
            args.Damage *= state.DamageMultiplier;

        if (state.PierceThreshold > FixedPoint2.Zero)
            args.ResistanceBypass = true;
    }

    private void OnGetMeleeAttackRate(EntityUid weapon, FSWeaponUpgradeStateComponent state, ref GetMeleeAttackRateEvent args)
    {
        if (state.AttackSpeedMultiplier > 1f)
            args.Multipliers *= state.AttackSpeedMultiplier;
    }

    private void OnHitscanDamage(EntityUid weapon, FSWeaponUpgradeStateComponent state, ref FSHitscanDamageModifyEvent args)
    {
        if (state.DamageMultiplier > 1f)
            args.Damage *= state.DamageMultiplier;
    }

    private void OnDamageExamine(EntityUid weapon, FSWeaponUpgradeStateComponent state, ref DamageExamineEvent args)
    {
        if (state.DamageMultiplier <= 1f || !HasComp<GunComponent>(weapon))
            return;

        args.Message.PushNewline();
        args.Message.AddMarkupOrThrow(Loc.GetString("fs-upgrade-damage-examine",
            ("percent", (int) MathF.Round((state.DamageMultiplier - 1f) * 100f))));
    }
}
