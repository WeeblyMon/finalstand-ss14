using System.Numerics;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Weapons;

// holding the trigger builds charge; releasing fires one shot scaled by how long it was held
public sealed class FSChargeShotSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan ReleaseGrace = TimeSpan.FromMilliseconds(120);

    private const float AimDistance = 10f;

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

        args.Cancelled = true;

        var comp = ent.Comp;
        comp.LastHeld = _timing.CurTime;
        comp.Shooter = args.User;
        if (comp.ChargeStart == null)
        {
            comp.ChargeStart = _timing.CurTime;
            comp.ChargeStream = _audio.Stop(comp.ChargeStream);
            comp.ChargeStream = _audio.PlayPvs(comp.ChargeSound, args.User,
                AudioParams.Default.WithLoop(true))?.Entity;
        }

        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } coords)
            return;

        var from = _transform.GetMapCoordinates(args.User);
        var to = _transform.ToMapCoordinates(coords);

        if (to.MapId == from.MapId && (to.Position - from.Position).LengthSquared() > 0.01f)
            comp.AimDirection = to.Position - from.Position;
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
        var aim = comp.AimDirection;

        comp.ChargeStart = null;
        comp.LastHeld = TimeSpan.Zero;
        comp.ChargeStream = _audio.Stop(comp.ChargeStream);

        if (shooter is { } user && !TerminatingOrDeleted(user) && aim.LengthSquared() > 0.01f)
        {
            var from = _transform.GetMapCoordinates(user);
            var target = new EntityCoordinates(_map.GetMapOrInvalid(from.MapId),
                from.Position + Vector2.Normalize(aim) * AimDistance);

            _gun.ClearFireCooldown((ent.Owner, gun), _timing.CurTime);

            _firing = ent.Owner;
            _gun.AttemptShoot(user, (ent.Owner, gun), target);
            _firing = null;
        }

        comp.Charge = 0f;
        comp.Shooter = null;
        Dirty(ent.Owner, comp);
    }
}
