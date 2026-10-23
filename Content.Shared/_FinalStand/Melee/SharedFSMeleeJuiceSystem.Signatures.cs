using System.Numerics;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Components;

namespace Content.Shared._FinalStand.Melee;

// Signature upgrades and each weapon's guaranteed crit.
public abstract partial class SharedFSMeleeJuiceSystem
{
    private const float GrandSlamForce = 2f;
    private const float RunningStartCharge = 0.5f;
    private const int BracePierce = 2;
    private const float BraceForce = 2f;
    private const float BraceStill = 0.3f;
    private const float WoundedCrit = 0.5f;
    private const float ShotRetry = 0.15f;
    private const float TempestRadius = 1.6f;
    private static readonly TimeSpan TempestWindow = TimeSpan.FromSeconds(0.6);

    // The signature's strength on this weapon, or zero when it isn't owned.
    public float Signature(EntityUid weapon, FSMeleeSignature signature)
    {
        return TryComp<FSWeaponUpgradeStateComponent>(weapon, out var upgrades) && upgrades.Signatures.TryGetValue(signature, out var value)
            ? value
            : 0f;
    }

    private int RevCap(Entity<FSMeleeJuiceComponent> weapon)
    {
        return weapon.Comp.RevStacks + (int) Signature(weapon, FSMeleeSignature.Redline);
    }

    // Crits that hold for the whole swing; the per-target ones are TargetCrits.
    private bool SwingCrits(Entity<FSMeleeJuiceComponent> weapon, FSMeleeJuiceProfile profile, bool heavy, int rev)
    {
        if (!heavy && TryComp<FSMeleeRevComponent>(weapon, out var revving) && revving.CritReady)
        {
            revving.CritReady = false;
            Dirty(weapon, revving);
            return true;
        }

        return weapon.Comp.CritWhen switch
        {
            FSMeleeCritWhen.FullCharge => heavy && profile.FullCharge,
            FSMeleeCritWhen.MaxRev => !heavy && rev >= RevCap(weapon),
            _ => false,
        };
    }

    public bool TargetCrits(Entity<FSMeleeJuiceComponent> weapon, EntityUid target)
    {
        return weapon.Comp.CritWhen switch
        {
            FSMeleeCritWhen.Wounded => HealthLost(target) >= WoundedCrit,
            FSMeleeCritWhen.Downed => HasComp<KnockedDownComponent>(target),
            _ => false,
        };
    }

    // Brace: a thrust from a standing start drives through and hits twice as hard.
    private bool Braced(EntityUid user, Entity<FSMeleeJuiceComponent> weapon)
    {
        return Signature(weapon, FSMeleeSignature.Brace) > 0f
            && TryComp<PhysicsComponent>(user, out var body)
            && body.LinearVelocity.Length() < BraceStill;
    }

    // Coup de Grace: an execution readies the next swing at once.
    private void CoupDeGrace(EntityUid user, Entity<FSMeleeJuiceComponent> weapon)
    {
        if (Signature(weapon, FSMeleeSignature.CoupDeGrace) <= 0f || !TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return;

        melee.NextAttack = Timing.CurTime;
        DirtyField(weapon, melee, nameof(MeleeWeaponComponent.NextAttack));
    }

    // Spends one of the weapon's shots, or waits a moment if none are ready.
    private bool TakeShot(Entity<FSMeleeJuiceComponent> weapon, Vector2 aim)
    {
        if (!TryComp<MeleeWeaponComponent>(weapon, out var melee))
            return false;

        var now = Timing.CurTime;
        var shots = EnsureComp<FSMeleeShotStateComponent>(weapon);
        var ready = shots.Charges > 0;
        if (ready)
        {
            if (shots.Charges >= shots.MaxCharges)
                shots.NextCharge = now + TimeSpan.FromSeconds(weapon.Comp.ShotRecharge);

            shots.Charges--;
            shots.LastShot = now;
            shots.LastAim = aim.ToAngle();
            Dirty(weapon, shots);
        }

        melee.NextAttack = now + TimeSpan.FromSeconds(ready ? weapon.Comp.ShotInterval : ShotRetry);
        DirtyField(weapon, melee, nameof(MeleeWeaponComponent.NextAttack));
        return ready;
    }

    // Tempest: a light attack just after a sweep becomes a spinning cut round where it landed.
    private void TryTempest(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, MeleeHitEvent args)
    {
        if (Signature(weapon, FSMeleeSignature.Tempest) <= 0f
            || !TryComp<FSMeleeSweepComponent>(user, out var sweep)
            || sweep.TempestUsed
            || Timing.CurTime - sweep.Last > TempestWindow
            || TryComp<ItemToggleComponent>(weapon, out var toggle) && !toggle.Activated)
            return;

        sweep.TempestUsed = true;
        Dirty(user, sweep);

        var ring = new List<EntityUid>();
        var mobs = new HashSet<Entity<MobStateComponent>>();
        _sweepLookup.GetEntitiesInRange(Xform.GetMapCoordinates(user), TempestRadius, mobs);
        foreach (var mob in mobs)
        {
            if (mob.Owner != user
                && mob.Comp.CurrentState != MobState.Dead
                && !targets.Contains(mob)
                && !IsAlly(user, mob)
                && _sweepReach.InRangeUnobstructed(user, mob.Owner, TempestRadius + 0.5f))
                ring.Add(mob);
        }

        var damage = args.BaseDamage + args.BonusDamage;
        OnTempest(weapon, user, ring, damage);
        if (ring.Count > 0)
            OnHit(weapon, user, ring, Xform.GetWorldPosition(ring[0]) - Xform.GetWorldPosition(user), false, false, ring.Exists(t => WouldKill(t, damage.GetTotal())), Profile(weapon, false), args);
    }

    protected virtual void OnTempest(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> ring, DamageSpecifier damage)
    {
    }

    // Last Breath can finish anything knocked off its feet.
    protected virtual bool Airborne(EntityUid target)
    {
        return HasComp<KnockedDownComponent>(target);
    }
}
