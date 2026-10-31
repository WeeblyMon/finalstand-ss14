using System.Linq;
using System.Numerics;
using Content.Server._FinalStand.Deployables;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Weapons.Prediction;
using Content.Shared.CCVar;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons.Prediction;

public sealed partial class FSLagCompensationSystem : SharedFSLagCompensationSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private LagCompensationSystem _lag = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float MaxTargetSpeed = 8f;
    private const float BodyPadding = 1.5f;

    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<LagCompensationComponent> _lagQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    private ShotWindow? _window;
    private readonly List<ShotWindow> _pendingWindows = new();
    private readonly List<EntityUid> _sweepQueue = new();
    private readonly List<(float Fraction, EntityUid Target)> _hits = new();
    private readonly HashSet<EntityUid> _touching = new();
    private readonly HashSet<Entity<LagCompensationComponent>> _candidates = new();

    private bool _enabled;
    private float _margin;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _lagQuery = GetEntityQuery<LagCompensationComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeNetworkEvent<RequestShootEvent>(OnShootRequest,
            before: [typeof(SharedGunSystem)],
            after: [typeof(LagCompensationSystem)]);

        SubscribeLocalEvent<ProjectileComponent, FSProjectileLaunchedEvent>(OnLaunched);
        SubscribeLocalEvent<FSRewoundProjectileComponent, PreventCollideEvent>(OnPreventCollide);

        Subs.CVar(_cfg, CCVars.FSLagCompensation, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.FSLagCompensationMargin, v => _margin = v, true);

        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
    }

    private void OnShootRequest(RequestShootEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        _window = new ShotWindow(args.SenderSession, user, _gun.ValidShotTick(ev.ShotTick), ev.Predicted);
        _pendingWindows.Add(_window);
    }

    private void OnLaunched(Entity<ProjectileComponent> ent, ref FSProjectileLaunchedEvent args)
    {
        // A sentry credits its deployer as the shooter, but nobody aimed it: there's no client view to rewind to,
        // and it mustn't join the deployer's shot window either.
        if (args.Shooter is not { } shooter
            || !_actorQuery.TryComp(shooter, out var actor)
            || HasComp<FSDeployableProjectileComponent>(ent))
            return;

        if (_enabled)
        {
            var rewound = EnsureComp<FSRewoundProjectileComponent>(ent);
            rewound.Shooter = shooter;
            rewound.RewindTicks = _lag.GetRewindTicks(actor.PlayerSession);
            rewound.Touching.Clear();
        }

        if (_window is { } window && window.User == shooter)
            window.Launched.Add(ent);
    }

    private void OnPreventCollide(Entity<FSRewoundProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (!_enabled || RaisingPreventCollide || args.Cancelled || !_lagQuery.HasComp(args.OtherEntity))
            return;

        if (_fixturesQuery.TryComp(ent, out var fixtures)
            && fixtures.Fixtures.TryGetValue(SharedProjectileSystem.ProjectileFixture, out var projectileFixture)
            && ReferenceEquals(args.OurFixture, projectileFixture))
        {
            args.Cancelled = true;
        }
    }

    public override bool IsPredictedShot(EntityUid? shooter)
    {
        return shooter != null && _window is { Predicted: true } window && window.User == shooter;
    }

    public ICommonSession? GetHiddenViewer(EntityUid projectile)
    {
        if (!TryComp<FSShotTwinComponent>(projectile, out var twin) || !twin.Hideable || twin.ContactShown)
            return null;

        twin.ContactShown = true;

        return _actorQuery.TryComp(twin.Shooter, out var actor) ? actor.PlayerSession : null;
    }

    public override List<RayCastResults> IntersectRay(MapId mapId, CollisionRay ray, float maxLength, EntityUid? ignored, EntityUid? shooter)
    {
        var results = base.IntersectRay(mapId, ray, maxLength, ignored, shooter);

        if (!_enabled || shooter == null || !_actorQuery.TryComp(shooter, out var actor))
            return results;

        var rewind = _lag.GetRewindTicks(actor.PlayerSession);
        if (rewind <= 0)
            return results;

        results.RemoveAll(r => _lagQuery.HasComp(r.HitEntity));

        var direction = ray.Direction.Normalized();
        var end = ray.Position + direction * maxLength;
        var tick = _timing.CurTick.Value - rewind;

        _candidates.Clear();
        _lookup.GetEntitiesInRange(mapId, ray.Position + direction * (maxLength / 2f), maxLength / 2f + SearchPadding(rewind), _candidates);

        foreach (var candidate in _candidates)
        {
            if (candidate.Owner == ignored || TerminatingOrDeleted(candidate))
                continue;

            var past = _lag.GetPastMapCoordinates(candidate, tick, candidate.Comp);
            if (past.MapId != mapId
                || !TryGetBodyBounds(candidate, past.Position, ray.CollisionMask, out var bounds)
                || !SegmentHitsBox(ray.Position, end, bounds.Enlarged(_margin), out var fraction))
            {
                continue;
            }

            var distance = fraction * maxLength;
            results.Add(new RayCastResults(distance, ray.Position + direction * distance, candidate));
        }

        results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return results;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _window = null;
        foreach (var window in _pendingWindows)
        {
            FinalizeWindow(window);
        }

        _pendingWindows.Clear();

        if (!_enabled)
            return;

        _sweepQueue.Clear();
        var query = EntityQueryEnumerator<FSRewoundProjectileComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _sweepQueue.Add(uid);
        }

        var dt = (float) _timing.TickPeriod.TotalSeconds;
        foreach (var uid in _sweepQueue)
        {
            Sweep(uid, dt);
        }
    }

    private void FinalizeWindow(ShotWindow window)
    {
        if (!window.Predicted || window.Launched.Count == 0)
            return;

        List<int>? voided = null;

        for (var i = 0; i < window.Launched.Count; i++)
        {
            var uid = window.Launched[i];
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            {
                (voided ??= new List<int>()).Add(i);
                continue;
            }

            var twin = EnsureComp<FSShotTwinComponent>(uid);
            twin.Shooter = window.User;
            twin.Tick = window.Tick;
            twin.Index = i;
            twin.Hideable = !AltersFlight(uid);
            Dirty(uid, twin);
        }

        if (voided != null)
            RaiseNetworkEvent(new FSShotVoidedEvent(window.Tick, voided), window.Session);
    }

    private bool AltersFlight(EntityUid uid)
    {
        return HasComp<FSHomingProjectileComponent>(uid) || HasComp<FSRicochetComponent>(uid);
    }

    private void Sweep(EntityUid uid, float dt)
    {
        if (TerminatingOrDeleted(uid)
            || EntityManager.IsQueuedForDeletion(uid)
            || !TryComp<FSRewoundProjectileComponent>(uid, out var rewound)
            || !TryComp<ProjectileComponent>(uid, out var projectile)
            || projectile.ProjectileSpent
            || !_physicsQuery.TryComp(uid, out var body)
            || !TryGetProjectileShape(uid, out var projectileFixture, out var halfWidth, out var reach))
        {
            return;
        }

        var velocity = Physics.GetMapLinearVelocity(uid, body);
        var speed = velocity.Length();
        if (speed < 0.1f)
            return;

        var origin = _transform.GetMapCoordinates(uid);
        if (origin.MapId == MapId.Nullspace)
            return;

        var direction = velocity / speed;
        var length = speed * dt + reach;
        var start = origin.Position;
        var mask = projectileFixture.CollisionMask;
        var limit = WallDistance(uid, rewound.Shooter, body, projectileFixture, origin.MapId, start, direction, length, mask);

        var nowTick = _timing.CurTick.Value - rewound.RewindTicks;
        _hits.Clear();
        _touching.Clear();
        _candidates.Clear();
        _lookup.GetEntitiesInRange(origin.MapId, start + direction * (length / 2f), length / 2f + SearchPadding(rewound.RewindTicks + 1), _candidates);

        foreach (var candidate in _candidates)
        {
            var target = candidate.Owner;
            if (target == uid || target == rewound.Shooter || TerminatingOrDeleted(target))
                continue;

            var from = _lag.GetPastMapCoordinates(target, nowTick, candidate.Comp);
            var to = _lag.GetPastMapCoordinates(target, nowTick + 1, candidate.Comp);
            if (from.MapId != origin.MapId || to.MapId != origin.MapId)
                continue;

            if (!TryGetBodyBounds(target, Vector2.Zero, mask, out var bounds))
                continue;

            var relativeStart = start - from.Position;
            var relativeEnd = start + direction * length - to.Position;
            if (!SegmentHitsBox(relativeStart, relativeEnd, bounds.Enlarged(halfWidth + _margin), out var fraction)
                || fraction * length > limit)
            {
                continue;
            }

            if (rewound.Touching.Contains(target))
                _touching.Add(target);
            else
                _hits.Add((fraction, target));
        }

        _hits.Sort((a, b) => a.Fraction.CompareTo(b.Fraction));

        foreach (var (_, target) in _hits)
        {
            if (projectile.ProjectileSpent || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
                break;

            if (TerminatingOrDeleted(target)
                || !_physicsQuery.TryComp(target, out var targetBody)
                || !TryGetHitFixture(target, mask, out var targetFixtureId, out var targetFixture)
                || IsCollisionPrevented(uid, body, projectileFixture, target, targetBody, targetFixture))
            {
                continue;
            }

            _touching.Add(target);

            var ours = FSStartCollideFactory.Create(uid, target, SharedProjectileSystem.ProjectileFixture, targetFixtureId,
                projectileFixture, targetFixture, body, targetBody, direction);
            RaiseLocalEvent(uid, ref ours, true);

            if (TerminatingOrDeleted(target) || TerminatingOrDeleted(uid))
                continue;

            var theirs = FSStartCollideFactory.Create(target, uid, targetFixtureId, SharedProjectileSystem.ProjectileFixture,
                targetFixture, projectileFixture, targetBody, body, -direction);
            RaiseLocalEvent(target, ref theirs, true);
        }

        rewound.Touching.Clear();
        rewound.Touching.UnionWith(_touching);
    }

    private float SearchPadding(double rewindTicks)
    {
        return (float) (rewindTicks * _timing.TickPeriod.TotalSeconds) * MaxTargetSpeed + BodyPadding;
    }

    private float WallDistance(
        EntityUid uid,
        EntityUid shooter,
        PhysicsComponent body,
        Robust.Shared.Physics.Dynamics.Fixture projectileFixture,
        MapId mapId,
        Vector2 start,
        Vector2 direction,
        float length,
        int mask)
    {
        var obstacles = Physics.IntersectRayWithPredicate(mapId,
            new CollisionRay(start, direction, mask),
            (this, uid, shooter),
            static (ent, state) => ent == state.uid || ent == state.shooter || state.Item1._lagQuery.HasComp(ent),
            length,
            false);

        foreach (var obstacle in obstacles)
        {
            if (!_physicsQuery.TryComp(obstacle.HitEntity, out var obstacleBody)
                || !TryGetHitFixture(obstacle.HitEntity, mask, out _, out var obstacleFixture)
                || IsCollisionPrevented(uid, body, projectileFixture, obstacle.HitEntity, obstacleBody, obstacleFixture))
            {
                continue;
            }

            return obstacle.Distance;
        }

        return length;
    }

    private sealed class ShotWindow(ICommonSession session, EntityUid user, GameTick tick, bool predicted)
    {
        public readonly ICommonSession Session = session;
        public readonly EntityUid User = user;
        public readonly GameTick Tick = tick;
        public readonly bool Predicted = predicted;
        public readonly List<EntityUid> Launched = new();
    }
}
