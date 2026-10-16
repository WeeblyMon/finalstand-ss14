using System.Numerics;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Weapons.Prediction;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Effects;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Weapons.Prediction;

public sealed partial class FSShotPredictionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSHitscanCoordSystem _hitscanCoords = default!;
    [Dependency] private FSLagCompensationSystem _lagCompensation = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private HitscanBasicRaycastSystem _hitscanRaycast = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedProjectileSystem _projectiles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(3);
    private const float DefaultLifetime = 10f;
    private const float SearchPadding = 1.5f;
    private const float ContinueSlack = 0.75f;
    private const float MatchLateral = 0.35f;
    private const float MatchDot = 0.995f;
    private const float MatchSpeed = 0.05f;
    private const float DriftLateral = 0.5f;

    private readonly Dictionary<(GameTick Tick, int Index), PredictedShot> _shots = new();
    private readonly List<(GameTick Tick, int Index)> _expired = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<(float Distance, EntityUid Target)> _mobHits = new();

    private EntityQuery<FSFriendlyFireComponent> _friendlyQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    private GameTick _indexTick;
    private int _nextIndex;
    private bool _enabled;
    private float _margin;

    public TimeSpan? LastServerBulletDelay { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        _friendlyQuery = GetEntityQuery<FSFriendlyFireComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeNetworkEvent<FSShotVoidedEvent>(OnShotVoided);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.CVar(_cfg, CCVars.FSShotPrediction, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.FSLagCompensationMargin, v => _margin = v, true);
    }

    public bool CanPredict(EntityUid gun)
    {
        return _enabled && _lagCompensation.CanPredictShots(gun);
    }

    public bool ShouldPredict(EntityUid? user, EntityUid gun)
    {
        return _timing.IsFirstTimePredicted && user != null && user == _player.LocalEntity && CanPredict(gun);
    }

    public void PredictProjectiles(
        Entity<GunComponent> gun,
        EntityUid user,
        EntityUid? ammo,
        EntProtoId? prototype,
        MapCoordinates from,
        Angle aimAngle,
        Vector2 shotDirection,
        GameTick tick)
    {
        var protoId = ammo != null ? MetaData(ammo.Value).EntityPrototype?.ID : prototype?.Id;
        if (protoId == null || !_protoMan.TryIndex<EntityPrototype>(protoId, out var proto))
            return;

        ProjectileSpreadComponent? spread = null;
        if (ammo != null)
            TryComp(ammo, out spread);
        else
            proto.TryGetComponent(out spread, Factory);

        var muzzleShift = shotDirection.LengthSquared() > 0f ? shotDirection.Normalized() * SharedGunSystem.MuzzleOffset : Vector2.Zero;

        if (spread == null)
        {
            SpawnCosmetic(gun, user, proto, from, shotDirection, muzzleShift, tick);
            return;
        }

        var spreadEvent = new GunGetAmmoSpreadEvent(spread.Spread);
        RaiseLocalEvent(gun, ref spreadEvent);

        var angles = SharedGunSystem.LinearSpread(aimAngle - spreadEvent.Spread / 2, aimAngle + spreadEvent.Spread / 2, spread.Count);
        SpawnCosmetic(gun, user, proto, from, angles[0].ToVec(), muzzleShift, tick);

        if (!_protoMan.TryIndex(spread.Proto, out var pelletProto))
            return;

        for (var i = 1; i < spread.Count; i++)
        {
            SpawnCosmetic(gun, user, pelletProto, from, angles[i].ToVec(), muzzleShift, tick);
        }
    }

    private void SpawnCosmetic(Entity<GunComponent> gun, EntityUid user, EntityPrototype proto, MapCoordinates from, Vector2 direction, Vector2 muzzleShift, GameTick tick)
    {
        if (!proto.Components.ContainsKey(Factory.GetComponentName<ProjectileComponent>()) || direction.LengthSquared() < 0.0001f)
            return;

        if (_indexTick != tick)
        {
            _indexTick = tick;
            _nextIndex = 0;
        }

        var key = (tick, _nextIndex++);
        direction = direction.Normalized();
        from = new MapCoordinates(from.Position + muzzleShift, from.MapId);

        if (proto.Components.ContainsKey(Factory.GetComponentName<FSRicochetComponent>()))
            return;

        var uid = Spawn(proto.ID, from);
        if (!TryComp<ProjectileComponent>(uid, out var projectile)
            || !_physicsQuery.TryComp(uid, out var body)
            || !_lagCompensation.TryGetProjectileShape(uid, out var fixture, out var halfWidth, out var reach))
        {
            Del(uid);
            return;
        }

        _physics.SetCanCollide(uid, false, body: body);
        _projectiles.SetShooter(uid, projectile, user);
        projectile.Weapon = gun;
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
            _gun.SetCosmeticTarget(uid, target);

        _transform.SetWorldRotationNoLerp(uid, direction.ToWorldAngle() + projectile.Angle);

        var speed = gun.Comp.ProjectileSpeedModified;
        var lifetime = TryComp<TimedDespawnComponent>(uid, out var despawn) ? despawn.Lifetime : DefaultLifetime;

        if (_shots.Remove(key, out var stale))
            End(stale, stale.Traveled);

        _shots[key] = new PredictedShot
        {
            Cosmetic = uid,
            Body = body,
            Fixture = fixture,
            Shooter = user,
            Vehicle = CompOrNull<VehicleOperatorComponent>(user)?.Vehicle,
            Map = from.MapId,
            Origin = from.Position,
            Direction = direction,
            Speed = speed,
            MaxDistance = speed * lifetime,
            HalfWidth = halfWidth,
            Reach = reach,
            Mask = fixture.CollisionMask,
            ImpactEffect = projectile.ImpactEffect,
            SoundHit = projectile.SoundHit,
            Start = _timing.RealTime,
        };
    }

    public void PredictHitscan(Entity<GunComponent> gun, EntityUid? ammo, EntityUid user, EntityCoordinates fromCoordinates, Vector2 direction)
    {
        if (ammo is not { } hitscan
            || !_lagCompensation.CanPredictHitscan(hitscan)
            || !TryComp<HitscanBasicRaycastComponent>(hitscan, out var raycast)
            || direction.LengthSquared() < 0.0001f)
        {
            return;
        }

        direction = direction.Normalized();
        var from = _transform.ToMapCoordinates(fromCoordinates);
        var ray = new CollisionRay(from.Position, direction, (int) raycast.CollisionMask);
        var results = _physics.IntersectRay(from.MapId, ray, raycast.MaxDistance, user, false);

        var friendly = _friendlyQuery.HasComp(user);
        var inContainer = _container.IsEntityOrParentInContainer(user);
        EntityUid? vehicle = _container.TryGetContainingContainer(user, out var holder) ? holder.Owner : null;
        RayCastResults? hit = null;

        foreach (var result in results)
        {
            if (result.HitEntity == vehicle)
                continue;

            if (!inContainer)
            {
                if (result.HitEntity != gun.Comp.Target && CompOrNull<RequireProjectileTargetComponent>(result.HitEntity)?.Active == true)
                    continue;

                if (friendly && _friendlyQuery.HasComp(result.HitEntity))
                    continue;
            }

            hit = result;
            break;
        }

        var distance = hit?.Distance ?? raycast.MaxDistance;

        if (HasComp<FSHarvesterComponent>(hitscan))
        {
            var origin = Transform(user).Coordinates;
            if (hit is { } harvested)
                distance = (_transform.GetWorldPosition(harvested.HitEntity) - _transform.ToMapCoordinates(origin).Position).Length();

            var (coords, angle) = _hitscanCoords.ToGridRelative(origin, direction.ToAngle());
            RaiseLocalEvent(new FSHarvesterBeamFiredEvent(GetNetCoordinates(coords), (float) angle.Theta, distance, GetNetEntity(user)));
            return;
        }

        if (_hitscanRaycast.BuildEffects(fromCoordinates, distance, direction.ToAngle(), hitscan, user) is { } effects)
            RaiseLocalEvent(effects);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_shots.Count == 0)
            return;

        var now = _timing.RealTime;
        var local = _player.LocalEntity;

        var twins = EntityQueryEnumerator<FSShotTwinComponent>();
        while (twins.MoveNext(out var twinUid, out var twin))
        {
            if (twin.Shooter != local
                || !_shots.TryGetValue((twin.Tick, twin.Index), out var shot)
                || shot.Twin != null)
            {
                continue;
            }

            OnTwinSeen(shot, twinUid, twin);
        }

        foreach (var (key, shot) in _shots)
        {
            if (!shot.Ended)
                Advance(shot, now);

            Reconcile(shot);

            if (!shot.Ended || now - shot.EndedAt <= Retention)
                continue;

            if (shot.Twin is { } twin && !shot.TwinShown && !shot.TwinGone && !TerminatingOrDeleted(twin))
                SetTwinVisible(shot, true);

            _expired.Add(key);
        }

        foreach (var key in _expired)
        {
            _shots.Remove(key);
        }

        _expired.Clear();
    }

    private void Advance(PredictedShot shot, TimeSpan now)
    {
        if (TerminatingOrDeleted(shot.Cosmetic) || Transform(shot.Cosmetic).MapID != shot.Map)
        {
            End(shot, shot.Traveled);
            return;
        }

        var target = MathF.Min(shot.Speed * (float) (now - shot.Start).TotalSeconds, shot.MaxDistance);

        if (shot.ForcedEnd is { } forced && target >= forced)
        {
            ImpactEffect(shot, shot.Origin + shot.Direction * forced);
            End(shot, forced);
            return;
        }

        if (target <= shot.Traveled)
            return;

        var from = shot.Traveled;
        var span = target - from + shot.Reach;
        var start = shot.Origin + shot.Direction * from;
        var wall = WallHit(shot, start, span);
        var limit = wall?.Distance ?? span;

        if (MobHit(shot, start, span, limit) is { } mob)
        {
            var contact = start + shot.Direction * mob.Distance;
            OnMobHit(shot, mob.Target, contact);
            End(shot, from + mob.Distance);
            return;
        }

        if (wall is { } blocked)
        {
            var contact = start + shot.Direction * blocked.Distance;
            OnWallHit(shot, blocked.Target, contact);
            End(shot, from + blocked.Distance);
            return;
        }

        if (target >= shot.MaxDistance)
        {
            End(shot, target);
            return;
        }

        shot.Traveled = target;
        _transform.SetWorldPosition(shot.Cosmetic, shot.Origin + shot.Direction * target);
    }

    private (float Distance, EntityUid Target)? WallHit(PredictedShot shot, Vector2 start, float span)
    {
        var results = _physics.IntersectRayWithPredicate(shot.Map,
            new CollisionRay(start, shot.Direction, shot.Mask),
            (this, shot),
            static (ent, state) => ent == state.shot.Cosmetic || ent == state.shot.Shooter || ent == state.shot.Vehicle || state.Item1._mobQuery.HasComp(ent),
            span,
            false);

        foreach (var result in results)
        {
            if (!_physicsQuery.TryComp(result.HitEntity, out var body)
                || !_lagCompensation.TryGetHitFixture(result.HitEntity, shot.Mask, out _, out var fixture)
                || _lagCompensation.IsCollisionPrevented(shot.Cosmetic, shot.Body, shot.Fixture, result.HitEntity, body, fixture))
            {
                continue;
            }

            return (result.Distance, result.HitEntity);
        }

        return null;
    }

    private (float Distance, EntityUid Target)? MobHit(PredictedShot shot, Vector2 start, float span, float limit)
    {
        var end = start + shot.Direction * span;
        _mobs.Clear();
        _mobHits.Clear();
        _lookup.GetEntitiesInRange(shot.Map, start + shot.Direction * (span / 2f), span / 2f + SearchPadding, _mobs);

        foreach (var mob in _mobs)
        {
            if (mob.Owner == shot.Shooter || mob.Owner == shot.Vehicle || TerminatingOrDeleted(mob))
                continue;

            var position = _transform.GetMapCoordinates(mob);
            if (position.MapId != shot.Map
                || !_lagCompensation.TryGetBodyBounds(mob, position.Position, shot.Mask, out var bounds)
                || !SharedFSLagCompensationSystem.SegmentHitsBox(start, end, bounds.Enlarged(shot.HalfWidth + _margin), out var fraction))
            {
                continue;
            }

            var distance = fraction * span;
            if (distance <= limit)
                _mobHits.Add((distance, mob));
        }

        _mobHits.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        foreach (var (distance, target) in _mobHits)
        {
            if (!_physicsQuery.TryComp(target, out var body)
                || !_lagCompensation.TryGetHitFixture(target, shot.Mask, out _, out var fixture)
                || _lagCompensation.IsCollisionPrevented(shot.Cosmetic, shot.Body, shot.Fixture, target, body, fixture))
            {
                continue;
            }

            return (distance, target);
        }

        return null;
    }

    private void OnMobHit(PredictedShot shot, EntityUid target, Vector2 contact)
    {
        ImpactEffect(shot, contact);
        RaiseLocalEvent(new ColorFlashEffectEvent(Color.Red, new List<NetEntity> { GetNetEntity(target) }));
        _audio.PlayEntity(shot.SoundHit, Filter.Local(), target, false);
    }

    private void OnWallHit(PredictedShot shot, EntityUid target, Vector2 contact)
    {
        ImpactEffect(shot, contact);
        _audio.PlayEntity(shot.SoundHit, Filter.Local(), target, false);
    }

    private void ImpactEffect(PredictedShot shot, Vector2 position)
    {
        if (shot.ImpactEffect is not { } effect)
            return;

        var coordinates = _transform.ToCoordinates(new MapCoordinates(position, shot.Map));
        RaiseLocalEvent(new ImpactEffectEvent(effect, GetNetCoordinates(coordinates)));
    }

    private void End(PredictedShot shot, float distance)
    {
        shot.Ended = true;
        shot.EndDistance = distance;
        shot.EndedAt = _timing.RealTime;

        if (!TerminatingOrDeleted(shot.Cosmetic))
            QueueDel(shot.Cosmetic);
    }

    private void OnTwinSeen(PredictedShot shot, EntityUid twin, FSShotTwinComponent comp)
    {
        shot.Twin = twin;
        LastServerBulletDelay = _timing.RealTime - shot.Start;

        if (comp.Hideable && Matches(shot, twin))
        {
            SetTwinVisible(shot, false);
            return;
        }

        shot.TwinShown = true;
        if (!shot.Ended)
            End(shot, shot.Traveled);
    }

    private bool Matches(PredictedShot shot, EntityUid twin)
    {
        if (!_physicsQuery.TryComp(twin, out var body))
            return false;

        var position = _transform.GetMapCoordinates(twin);
        if (position.MapId != shot.Map)
            return false;

        var velocity = _physics.GetMapLinearVelocity(twin, body);
        var speed = velocity.Length();
        if (speed < 0.01f || MathF.Abs(speed - shot.Speed) > shot.Speed * MatchSpeed)
            return false;

        return Vector2.Dot(velocity / speed, shot.Direction) >= MatchDot && Lateral(shot, position.Position) <= MatchLateral;
    }

    private void Reconcile(PredictedShot shot)
    {
        if (shot.Twin is not { } twin || shot.TwinShown || shot.TwinGone)
            return;

        if (TerminatingOrDeleted(twin))
        {
            shot.TwinGone = true;
            if (shot.Ended)
                return;

            var along = Along(shot, shot.TwinLastPosition);
            if (along > shot.Traveled)
                shot.ForcedEnd = along;
            else
                End(shot, shot.Traveled);
            return;
        }

        var position = _transform.GetMapCoordinates(twin);
        if (position.MapId != shot.Map)
            return;

        shot.TwinLastPosition = position.Position;

        if (shot.Ended)
        {
            if (Along(shot, position.Position) > shot.EndDistance + ContinueSlack)
                SetTwinVisible(shot, true);
            else
                SetTwinVisible(shot, false);
            return;
        }

        if (Lateral(shot, position.Position) > DriftLateral)
        {
            End(shot, shot.Traveled);
            SetTwinVisible(shot, true);
            return;
        }

        SetTwinVisible(shot, false);
    }

    private void SetTwinVisible(PredictedShot shot, bool visible)
    {
        if (shot.Twin is not { } twin)
            return;

        if (visible)
            shot.TwinShown = true;

        if (TryComp<SpriteComponent>(twin, out var sprite) && sprite.Visible != visible)
            _sprite.SetVisible((twin, sprite), visible);

        if (!TryComp<PointLightComponent>(twin, out var light))
            return;

        if (!visible && light.Enabled)
        {
            shot.TwinLightHidden = true;
            _lights.SetEnabled(twin, false, light);
        }
        else if (visible && shot.TwinLightHidden)
        {
            shot.TwinLightHidden = false;
            _lights.SetEnabled(twin, true, light);
        }
    }

    private static float Along(PredictedShot shot, Vector2 position)
    {
        return Vector2.Dot(position - shot.Origin, shot.Direction);
    }

    private static float Lateral(PredictedShot shot, Vector2 position)
    {
        var offset = position - shot.Origin;
        return MathF.Abs(offset.X * shot.Direction.Y - offset.Y * shot.Direction.X);
    }

    private void OnShotVoided(FSShotVoidedEvent ev)
    {
        foreach (var index in ev.Indices)
        {
            if (!_shots.TryGetValue((ev.Tick, index), out var shot))
                continue;

            shot.TwinShown = true;
            if (!shot.Ended)
                End(shot, shot.Traveled);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var shot in _shots.Values)
        {
            if (!TerminatingOrDeleted(shot.Cosmetic))
                QueueDel(shot.Cosmetic);
        }

        _shots.Clear();
    }

    private sealed class PredictedShot
    {
        public EntityUid Cosmetic;
        public PhysicsComponent Body = default!;
        public Fixture Fixture = default!;
        public EntityUid Shooter;
        public EntityUid? Vehicle;
        public MapId Map;
        public Vector2 Origin;
        public Vector2 Direction;
        public float Speed;
        public float MaxDistance;
        public float HalfWidth;
        public float Reach;
        public int Mask;
        public EntProtoId? ImpactEffect;
        public SoundSpecifier? SoundHit;
        public TimeSpan Start;
        public float Traveled;
        public float? ForcedEnd;
        public bool Ended;
        public float EndDistance;
        public TimeSpan EndedAt;
        public EntityUid? Twin;
        public bool TwinShown;
        public bool TwinGone;
        public bool TwinLightHidden;
        public Vector2 TwinLastPosition;
    }
}
