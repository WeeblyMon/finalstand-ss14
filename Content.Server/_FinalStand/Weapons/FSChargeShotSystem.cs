using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Weapons.Visuals;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

// scales the released volley: damage, speed, pierce, bounces and pellet size all follow the charge
public sealed class FSChargeShotEffectsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSChargeShotComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(EntityUid uid, FSChargeShotComponent comp, AmmoShotEvent args)
    {
        var charge = comp.Charge;

        var damageMul = MathHelper.Lerp(comp.MinDamageMultiplier, comp.MaxDamageMultiplier, charge);
        var speedMul = MathHelper.Lerp(comp.MinSpeedMultiplier, comp.MaxSpeedMultiplier, charge);
        var scale = MathHelper.Lerp(comp.MinPelletScale, comp.MaxPelletScale, charge);
        var pierce = (int) MathF.Round(comp.MaxBonusPierce * charge);
        var bounces = comp.MinBounces + (int) MathF.Round((comp.MaxBounces - comp.MinBounces) * charge);

        foreach (var projUid in args.FiredProjectiles)
        {
            if (TryComp<ProjectileComponent>(projUid, out var proj))
            {
                proj.Damage *= damageMul;

                if (pierce > 0 || bounces > 0)
                    proj.DeleteOnCollide = false;
            }

            if (pierce > 0)
                EnsureComp<FSPierceComponent>(projUid).RemainingPierces = pierce;

            EnsureComp<FSRicochetComponent>(projUid).Bounces = bounces;

            if (TryComp<PhysicsComponent>(projUid, out var body))
                _physics.SetLinearVelocity(projUid, body.LinearVelocity * speedMul, body: body);

            _appearance.SetData(projUid, FSChargeShotVisuals.PelletScale, scale);
        }
    }
}
