using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.Damage.Systems;
using Content.Shared._FinalStand.Perks;
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

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSPerkBuffSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSCombatMedicSystem _combatMedic = default!;
    [Dependency] private FSBerserkerSystem _berserker = default!;
    [Dependency] private FSUndyingSystem _undying = default!;
    [Dependency] private FSPerkAmmoSystem _perkAmmo = default!;

    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";
    private static readonly ProtoId<TagPrototype> ShotgunTag = "WeaponGunShotgun";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHit);
        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnGunShot);
        SubscribeLocalEvent<MobMoverComponent, RefreshMovementSpeedModifiersEvent>(OnLightweight);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnSwordAndShieldDamage);
    }

    private void OnProjectileHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Shooter == null) return;
        if (!_mind.TryGetMind(ev.Shooter.Value, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var isLauncher = ev.Weapon.HasValue && _tags.HasTag(ev.Weapon.Value, LauncherTag);

        var spLevel = augs.GetSlottedLevel("StoppingPower");
        if (spLevel > 0 && !isLauncher)
            ev.AdditionalMultiplier *= 1f + spLevel * FSPerkBonusConstants.StoppingPowerPerLevel;

        var profLevel = augs.GetSlottedLevel("Profiteer");
        if (profLevel > 0 && HasComp<WaveSpawnedTagComponent>(ev.Target) && !_undying.IsActive(ev.Shooter.Value))
            _wallet.GiveCredits(mindId, (int)(FSPerkBonusConstants.ProfiteerHitBase * profLevel * FSPerkBonusConstants.ProfiteerFraction));

        if (augs.GetSlottedLevel("DeathAura") > 0 && TryComp<FSDeathAuraComponent>(mindId, out var da) && da.Stacks > 0)
            ev.AdditionalMultiplier *= 1f + da.Stacks * FSPerkBonusConstants.DeathAuraPerStack;

        var gcLevel = augs.GetSlottedLevel("GlassCannon");
        if (gcLevel > 0)
            ev.AdditionalMultiplier *= 1f + gcLevel * FSPerkBonusConstants.GlassCannonPerLevel;

        if (augs.GetSlottedLevel("Pacifist") > 0)
            ev.AdditionalMultiplier *= 1f - FSPerkBonusConstants.PacifistPenalty;

        if (TryComp<FSOfficerBuffComponent>(mindId, out var ob) && _timing.CurTime < ob.EndTime)
            ev.AdditionalMultiplier *= 1f + ob.Level * FSPerkBonusConstants.OfficerBuffPerLevel;

        ev.AdditionalMultiplier *= _combatMedic.GetDamageMultiplier(mindId);
        ev.AdditionalMultiplier *= _berserker.GetMultiplier(ev.Shooter.Value, augs, ranged: true);

        var kbLevel = augs.GetSlottedLevel("KnockbackBlast");
        if (kbLevel > 0 && ev.Weapon.HasValue && _tags.HasTag(ev.Weapon.Value, ShotgunTag) && ev.Shooter.HasValue)
            _knockback.ApplyKnockback(ev.Target, ev.Shooter.Value, Math.Clamp(kbLevel, 1, 3));

        if (!ev.WasCrit) return;

        var bbLevel = augs.GetSlottedLevel("BackBreaker");
        if (bbLevel > 0 && ev.Shooter.HasValue)
            _knockback.ApplyKnockback(ev.Target, ev.Shooter.Value, Math.Clamp(bbLevel, 1, 3));

        var lbLevel = augs.GetSlottedLevel("LegBreaker");
        if (lbLevel > 0 && HasComp<StaminaComponent>(ev.Target))
            _stamina.TakeStaminaDamage(ev.Target, lbLevel * FSPerkBonusConstants.LegBreakerStaminaPerLevel, source: ev.Shooter);
    }

    public void ApplyGunModifiers(EntityUid holder, ref GunRefreshModifiersEvent args)
    {
        if (!_mind.TryGetMind(holder, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("BulletStorm");
        if (level <= 0) return;

        args.FireRate *= 1f + level * FSPerkBonusConstants.BulletStormPerLevel;
    }

    private void OnLightweight(EntityUid uid, MobMoverComponent mover, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var mult = 1f;

        var lwLevel = augs.GetSlottedLevel("Lightweight");
        if (lwLevel > 0)
            mult *= 1f + lwLevel * FSPerkBonusConstants.LightweightPerLevel;

        var sdLevel = augs.GetSlottedLevel("SpeedDemon");
        if (sdLevel > 0 && TryComp<FSSpeedDemonComponent>(mindId, out var sd) && sd.Stacks > 0)
            mult *= 1f + sd.Stacks * sdLevel * FSPerkBonusConstants.SpeedDemonPerLevel;

        var rampLevel = augs.GetSlottedLevel("Rampage");
        if (rampLevel > 0 && TryComp<FSRampageComponent>(mindId, out var ramp) && ramp.Stacks > 0)
            mult *= 1f + ramp.Stacks * rampLevel * FSPerkBonusConstants.RampageSpeedPerLevel;

        if (Math.Abs(mult - 1f) > 0.0001f)
            args.ModifySpeed(mult, mult);
    }

    private void OnGunShot(EntityUid uid, GunComponent _, AmmoShotEvent args)
    {
        var holder = Transform(uid).ParentUid;
        if (!holder.IsValid()) return;
        if (!_mind.TryGetMind(holder, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        _perkAmmo.OnShot(uid, holder, augs);

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
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var snsLevel = augs.GetSlottedLevel("SwordAndShield");
        if (snsLevel > 0)
            args.Damage *= 1f + snsLevel * FSPerkBonusConstants.SwordAndShieldPerLevel;

        if (augs.GetSlottedLevel("Pacifist") > 0)
            args.Damage *= 1f - FSPerkBonusConstants.PacifistPenalty;

        var gcLevel = augs.GetSlottedLevel("GlassCannon");
        if (gcLevel > 0)
            args.Damage *= 1f + gcLevel * FSPerkBonusConstants.GlassCannonPerLevel;

        args.Damage *= _combatMedic.GetDamageMultiplier(mindId);
        args.Damage *= _berserker.GetMultiplier(args.User, augs, ranged: false);
    }
}
