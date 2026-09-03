using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Weapons.Visuals;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.Weapons;

// scales the released volley: more damage, more pierce and bigger pellets the longer it was held
public sealed class FSChargeShotEffectsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSChargeShotComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<FSChargeShotComponent> ent, ref AmmoShotEvent args)
    {
        var comp = ent.Comp;
        var charge = comp.Charge;

        var damageMul = MathHelper.Lerp(comp.MinDamageMultiplier, comp.MaxDamageMultiplier, charge);
        var pierce = (int) MathF.Round(comp.MaxBonusPierce * charge);
        var scale = MathHelper.Lerp(comp.MinPelletScale, comp.MaxPelletScale, charge);

        foreach (var projUid in args.FiredProjectiles)
        {
            if (TryComp<ProjectileComponent>(projUid, out var proj))
                proj.Damage *= damageMul;

            if (pierce > 0)
            {
                if (TryComp<ProjectileComponent>(projUid, out var pierceProj))
                    pierceProj.DeleteOnCollide = false;

                EnsureComp<FSPierceComponent>(projUid).RemainingPierces = pierce;
            }

            EnsureComp<FSRicochetComponent>(projUid);
            _appearance.SetData(projUid, FSChargeShotVisuals.PelletScale, scale);
        }
    }
}
