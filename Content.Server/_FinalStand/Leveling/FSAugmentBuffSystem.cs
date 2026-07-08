using Content.Server._FinalStand.Augments;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Leveling;

public sealed class FSAugmentBuffSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;

    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";

    private const float BaseHitPayout = 30f;
    private const float ProfiteerFraction = 0.07f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHit);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnBulletStorm);
        SubscribeLocalEvent<MobMoverComponent, RefreshMovementSpeedModifiersEvent>(OnLightweight);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnSwordAndShieldDamage);
    }

    // Handles StoppingPower damage bonus and Profiteer on-hit money in one pass.
    private void OnProjectileHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Shooter == null) return;
        if (!_mind.TryGetMind(ev.Shooter.Value, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var isLauncher = ev.Weapon.HasValue && _tags.HasTag(ev.Weapon.Value, LauncherTag);

        var spLevel = augs.GetSlottedLevel("StoppingPower");
        if (spLevel > 0 && !isLauncher)
            ev.AdditionalMultiplier *= 1f + spLevel * 0.04f;

        var profLevel = augs.GetSlottedLevel("Profiteer");
        if (profLevel > 0)
            _wallet.GiveCredits(mindId, (int)(BaseHitPayout * profLevel * ProfiteerFraction));
    }

    private void OnBulletStorm(EntityUid uid, GunComponent gunComp, ref GunRefreshModifiersEvent args)
    {
        var holder = Transform(uid).ParentUid;
        if (!holder.IsValid()) return;
        if (!_mind.TryGetMind(holder, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("BulletStorm");
        if (level <= 0) return;

        args.FireRate *= 1f + level * 0.08f;
    }

    private void OnLightweight(EntityUid uid, MobMoverComponent mover, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("Lightweight");
        if (level <= 0) return;
        var mult = 1f + level * 0.03f;
        args.ModifySpeed(mult, mult);
    }

    // SwordAndShield: percentage damage boost applied during base damage calculation.
    private void OnSwordAndShieldDamage(EntityUid weapon, MeleeWeaponComponent melee, ref GetMeleeDamageEvent args)
    {
        if (!_mind.TryGetMind(args.User, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;
        var level = augs.GetSlottedLevel("SwordAndShield");
        if (level <= 0) return;
        args.Damage *= 1f + level * 0.05f;
    }

}
