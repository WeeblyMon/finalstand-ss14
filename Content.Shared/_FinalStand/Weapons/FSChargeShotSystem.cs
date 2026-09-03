using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Weapons;

// holding the trigger builds charge; releasing fires one shot scaled by how long it was held
public sealed class FSChargeShotSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    // The client only sends a shoot request while NextFire has elapsed, so the cooldown is cleared
    // every tick below to keep that stream running. A gap longer than this means the trigger is up.
    private static readonly TimeSpan ReleaseGrace = TimeSpan.FromMilliseconds(200);

    private EntityUid? _firing;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSChargeShotComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<FSChargeShotComponent> ent, ref AttemptShootEvent args)
    {
        if (_firing == ent.Owner)
            return;

        var comp = ent.Comp;
        comp.LastHeld = _timing.CurTime;
        comp.Shooter = args.User;

        if (TryComp<GunComponent>(ent, out var gun))
            comp.ShootCoordinates = gun.ShootCoordinates;

        comp.ChargeStart ??= _timing.CurTime;
        args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSChargeShotComponent, GunComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gun))
        {
            if (comp.ChargeStart is not { } start)
                continue;

            if (now - comp.LastHeld >= ReleaseGrace)
            {
                Release((uid, comp), gun);
                continue;
            }

            // Cancelling a shot pushes NextFire out by SafetyNextFire, which throttles the client's
            // requests to one every half second. Undo it so held triggers report every tick.
            _gun.ClearFireCooldown((uid, gun), now);

            var held = (float) (now - start).TotalSeconds;
            var charge = Math.Clamp(held / comp.MaxChargeTime, 0f, 1f);

            if (MathHelper.CloseTo(comp.Charge, charge, 0.01f))
                continue;

            comp.Charge = charge;
            Dirty(uid, comp);
        }
    }

    private void Release(Entity<FSChargeShotComponent> ent, GunComponent gun)
    {
        var comp = ent.Comp;
        var shooter = comp.Shooter;
        var coords = comp.ShootCoordinates;

        comp.ChargeStart = null;
        comp.LastHeld = TimeSpan.Zero;

        if (shooter is { } user && !TerminatingOrDeleted(user) && coords is { } target)
        {
            _gun.ClearFireCooldown((ent.Owner, gun), _timing.CurTime);

            _firing = ent.Owner;
            _gun.AttemptShoot(user, (ent.Owner, gun), target);
            _firing = null;
        }

        comp.Charge = 0f;
        comp.Shooter = null;
        comp.ShootCoordinates = null;
        Dirty(ent.Owner, comp);
    }
}
