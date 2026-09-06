using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._FinalStand.Weapons;

public sealed class FSChargeShotEffectsSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

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

            if (bounces > 0)
            {
                var ricochet = EnsureComp<FSRicochetComponent>(projUid);
                ricochet.Bounces = bounces;
                ricochet.DamageRetained = comp.BounceDamageRetained;
                ricochet.SpeedRetained = comp.BounceSpeedRetained;
                ricochet.Refund = comp.BounceRefund;
                ricochet.Crit = comp.BounceCrit;
                ricochet.Fracture = comp.Fracture;
            }
            else
            {
                RemComp<FSRicochetComponent>(projUid);
            }

            if (TryComp<PhysicsComponent>(projUid, out var body))
                _physics.SetLinearVelocity(projUid, body.LinearVelocity * speedMul, body: body);

            var pellet = EnsureComp<FSChargeShotPelletComponent>(projUid);
            pellet.Scale = scale;
            Dirty(projUid, pellet);
        }
    }
}
