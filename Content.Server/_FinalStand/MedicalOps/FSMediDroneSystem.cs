using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMediDroneSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private FSMediGunSystem _mediGun = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundSpecifier LowChargeSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg");

    private const float LowChargeLevel = 0.25f;

    private readonly HashSet<Entity<FSFriendlyFireComponent>> _candidates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMediDroneComponent, FSDeployableDeployedEvent>(OnDeployed);
    }

    private void OnDeployed(Entity<FSMediDroneComponent> ent, ref FSDeployableDeployedEvent args)
    {
        ent.Comp.Launcher = args.User;
        ent.Comp.OrbitAngle = (float) (_xform.GetWorldPosition(ent.Owner) - _xform.GetWorldPosition(args.User)).ToAngle().Theta;
        _xform.Unanchor(ent.Owner, Transform(ent.Owner));
        _audio.PlayPvs(LaunchSound, ent.Owner, AudioParams.Default.WithVolume(-4f));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMediDroneComponent, FSMediGunComponent>();
        while (query.MoveNext(out var uid, out var drone, out var gun))
        {
            if (drone.Launcher is not { } owner || TerminatingOrDeleted(owner))
            {
                QueueDel(uid);
                continue;
            }

            var level = _battery.GetChargeLevel(uid);
            UpdateVisuals((uid, drone), level);
            UpdateCountdown((uid, drone), gun);

            if (level <= LowChargeLevel && !drone.LowChargeWarned)
            {
                drone.LowChargeWarned = true;
                _audio.PlayPvs(LowChargeSound, uid, AudioParams.Default.WithVolume(-6f));
            }

            if (level <= 0f)
            {
                _mediGun.DisableAllConnections((uid, gun));
                _explosion.TriggerExplosive(uid, user: owner);
                continue;
            }

            if (now >= drone.NextScan)
            {
                drone.NextScan = now + drone.ScanInterval;
                Retarget((uid, drone), (uid, gun), owner);
            }

            Fly((uid, drone), owner, frameTime);
        }
    }

    private void Retarget(Entity<FSMediDroneComponent> drone, Entity<FSMediGunComponent> gun, EntityUid owner)
    {
        var target = _mobState.IsIncapacitated(owner) ? null : PickPatient(drone, gun.Comp, owner);

        if (target != drone.Comp.Target)
        {
            _mediGun.DisableAllConnections(gun);
            drone.Comp.Target = target;
        }

        if (target is not { } patient || gun.Comp.HealedEntities.Contains(patient))
            return;

        var distance = (_xform.GetWorldPosition(drone) - _xform.GetWorldPosition(patient)).Length();
        if (distance <= drone.Comp.HoverDistance + 1f)
            _mediGun.Link(gun, patient, owner);
    }

    private EntityUid? PickPatient(Entity<FSMediDroneComponent> drone, FSMediGunComponent gun, EntityUid owner)
    {
        var ownerCoords = Transform(owner).Coordinates;
        if (drone.Comp.Target is { } current && IsTreatable(current, gun, owner)
            && _xform.InRange(ownerCoords, Transform(current).Coordinates, drone.Comp.DetectRange))
            return current;

        _candidates.Clear();
        _lookup.GetEntitiesInRange(ownerCoords, drone.Comp.DetectRange, _candidates);

        EntityUid? best = null;
        var worst = 0f;
        foreach (var candidate in _candidates)
        {
            if (!IsTreatable(candidate, gun, owner))
                continue;

            var damage = (float) _damageable.GetTotalDamage(candidate.Owner);
            if (damage <= worst)
                continue;

            worst = damage;
            best = candidate.Owner;
        }

        return best;
    }

    private bool IsTreatable(EntityUid patient, FSMediGunComponent gun, EntityUid owner)
    {
        return patient != owner
               && !TerminatingOrDeleted(patient)
               && !_mobState.IsDead(patient)
               && TryComp<DamageableComponent>(patient, out var damageable)
               && damageable.TotalDamage > 0
               && _mediGun.GetHealScale((patient, damageable), gun) > 0f;
    }

    private void Fly(Entity<FSMediDroneComponent> drone, EntityUid owner, float frameTime)
    {
        var xform = Transform(drone);
        var ownerXform = Transform(owner);
        if (xform.MapID != ownerXform.MapID)
        {
            _xform.SetCoordinates(drone, ownerXform.Coordinates);
            return;
        }

        var position = _xform.GetWorldPosition(xform);
        var ownerPosition = _xform.GetWorldPosition(ownerXform);

        if ((position - ownerPosition).Length() > drone.Comp.LeashRange)
            drone.Comp.Target = null;

        var (anchor, radius) = drone.Comp.Target is { } target && !TerminatingOrDeleted(target)
            ? (_xform.GetWorldPosition(target), drone.Comp.HoverDistance)
            : (ownerPosition, drone.Comp.FollowDistance);

        drone.Comp.OrbitAngle = (drone.Comp.OrbitAngle + drone.Comp.OrbitSpeed * frameTime) % MathF.Tau;
        var goal = anchor + new Vector2(MathF.Cos(drone.Comp.OrbitAngle), MathF.Sin(drone.Comp.OrbitAngle)) * radius;

        var toGoal = goal - position;
        var gap = toGoal.Length();
        if (gap < 0.001f)
            return;

        var step = MathF.Min(gap, drone.Comp.Speed * frameTime);
        _xform.SetWorldPosition(drone, position + toGoal / gap * step);
    }

    private void UpdateCountdown(Entity<FSMediDroneComponent> drone, FSMediGunComponent gun)
    {
        if (gun.BatteryWithdraw <= 0f)
            return;

        var perSecond = gun.BatteryWithdraw / gun.Frequency;
        var seconds = (int) MathF.Ceiling(_battery.GetCharge(drone.Owner) / perSecond);
        var max = (int) MathF.Ceiling(Comp<BatteryComponent>(drone.Owner).MaxCharge / perSecond);

        if (seconds == drone.Comp.SecondsLeft && max == drone.Comp.MaxSeconds)
            return;

        drone.Comp.SecondsLeft = seconds;
        drone.Comp.MaxSeconds = max;
        Dirty(drone);
    }

    private void UpdateVisuals(Entity<FSMediDroneComponent> drone, float level)
    {
        var charge = level switch
        {
            <= 0f => FSMediDroneCharge.Empty,
            <= 0.25f => FSMediDroneCharge.Quarter,
            <= 0.5f => FSMediDroneCharge.Half,
            <= 0.75f => FSMediDroneCharge.ThreeQuarters,
            _ => FSMediDroneCharge.Full,
        };

        if (charge == drone.Comp.LastCharge)
            return;

        drone.Comp.LastCharge = charge;
        _appearance.SetData(drone, FSMediDroneVisuals.Charge, charge);
    }
}
