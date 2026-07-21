using Content.Server._FinalStand.Augments;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Upgrades;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.Damage.Systems;
using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Leveling;

public sealed class FSAugmentBuffSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";
    private static readonly ProtoId<TagPrototype> ShotgunTag = "WeaponGunShotgun";

    private const float BaseHitPayout = 30f;
    private const float ProfiteerFraction = 0.07f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHit);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnBulletStorm);
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnDeepImpact);
        SubscribeLocalEvent<MobMoverComponent, RefreshMovementSpeedModifiersEvent>(OnLightweight);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnSwordAndShieldDamage);
    }

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

        // Death Aura: stacks → outgoing damage bonus.
        if (TryComp<FSDeathAuraComponent>(mindId, out var da) && da.Stacks > 0)
            ev.AdditionalMultiplier *= 1f + da.Stacks * 0.02f;

        // Glass Cannon: flat outgoing bonus.
        var gcLevel = augs.GetSlottedLevel("GlassCannon");
        if (gcLevel > 0)
            ev.AdditionalMultiplier *= 1f + gcLevel * 0.07f;

        // Pacifist: outgoing penalty.
        if (augs.GetSlottedLevel("Pacifist") > 0)
            ev.AdditionalMultiplier *= 0.75f;

        // Officer buff: ally damage bonus.
        if (TryComp<FSOfficerBuffComponent>(mindId, out var ob) && _timing.CurTime < ob.EndTime)
            ev.AdditionalMultiplier *= 1f + ob.Level * 0.15f;

        // Knockback Blast: shotgun knockback on every pellet.
        var kbLevel = augs.GetSlottedLevel("KnockbackBlast");
        if (kbLevel > 0 && ev.Weapon.HasValue && _tags.HasTag(ev.Weapon.Value, ShotgunTag) && ev.Shooter.HasValue)
            _knockback.ApplyKnockback(ev.Target, ev.Shooter.Value, Math.Clamp(kbLevel, 1, 3));

        if (!ev.WasCrit) return;

        // Back Breaker: crit knockback.
        var bbLevel = augs.GetSlottedLevel("BackBreaker");
        if (bbLevel > 0 && ev.Shooter.HasValue)
            _knockback.ApplyKnockback(ev.Target, ev.Shooter.Value, Math.Clamp(bbLevel, 1, 3));

        // Leg Breaker: crit stamina drain — staggering via stamina works on NPCs unlike speed modifiers.
        var lbLevel = augs.GetSlottedLevel("LegBreaker");
        if (lbLevel > 0 && HasComp<StaminaComponent>(ev.Target))
            _stamina.TakeStaminaDamage(ev.Target, lbLevel * 25f, source: ev.Shooter);
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

        var mult = 1f;

        var lwLevel = augs.GetSlottedLevel("Lightweight");
        if (lwLevel > 0)
            mult *= 1f + lwLevel * 0.03f;

        // Speed Demon: kill-stack speed bonus.
        var sdLevel = augs.GetSlottedLevel("SpeedDemon");
        if (sdLevel > 0 && TryComp<FSSpeedDemonComponent>(mindId, out var sd) && sd.Stacks > 0)
            mult *= 1f + sd.Stacks * sdLevel * 0.01f;

        // Rampage: melee-kill speed bonus.
        var rampLevel = augs.GetSlottedLevel("Rampage");
        if (rampLevel > 0 && TryComp<FSRampageComponent>(mindId, out var ramp) && ramp.Stacks > 0)
            mult *= 1f + ramp.Stacks * rampLevel * 0.01f;

        if (Math.Abs(mult - 1f) > 0.0001f)
            args.ModifySpeed(mult, mult);
    }

    private void OnDeepImpact(EntityUid uid, GunComponent _, AmmoShotEvent args)
    {
        var holder = Transform(uid).ParentUid;
        if (!holder.IsValid()) return;
        if (!_mind.TryGetMind(holder, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("DeepImpact");
        if (level <= 0) return;

        foreach (var projUid in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projUid, out var proj)) continue;
            proj.DeleteOnCollide = false;
            var pierce = EnsureComp<FSPierceComponent>(projUid);
            pierce.RemainingPierces = Math.Max(pierce.RemainingPierces, level);
        }
    }

    private void OnSwordAndShieldDamage(EntityUid weapon, MeleeWeaponComponent melee, ref GetMeleeDamageEvent args)
    {
        if (!_mind.TryGetMind(args.User, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var snsLevel = augs.GetSlottedLevel("SwordAndShield");
        if (snsLevel > 0)
            args.Damage *= 1f + snsLevel * 0.05f;

        // Pacifist: melee damage penalty.
        if (augs.GetSlottedLevel("Pacifist") > 0)
            args.Damage *= 0.75f;

        // Glass Cannon: melee damage bonus.
        var gcLevel = augs.GetSlottedLevel("GlassCannon");
        if (gcLevel > 0)
            args.Damage *= 1f + gcLevel * 0.07f;
    }
}
