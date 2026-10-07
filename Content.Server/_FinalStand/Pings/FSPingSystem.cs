// Turns a ping request into a ping: works out what was pinged, rate-limits the sender, tells everyone, and opens a task for requests.
using Content.Server._FinalStand.Emplacement;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Repair;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Mech;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Pings;
using Content.Shared._FinalStand.RCD;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Pings;

public sealed partial class FSPingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSMedicPingSystem _medicPing = default!;
    [Dependency] private FSTaskBoardSystem _tasks = default!;
    [Dependency] private FSStructureHealthSystem _health = default!;

    private const float MaxRange = 24f;
    private const float TaskPingLife = 60f;
    private const float RepairThreshold = 0.7f;
    private const float NearbyRepairRange = 3f;
    private const int BucketSize = 3;
    private static readonly TimeSpan BucketRefill = TimeSpan.FromSeconds(4f / 3f);
    private static readonly TimeSpan SpamWindow = TimeSpan.FromSeconds(3);
    private const int SpamLimit = 5;
    private static readonly TimeSpan SpamMute = TimeSpan.FromSeconds(4);

    private readonly Dictionary<NetUserId, Limiter> _limits = new();
    private int _nextId;

    private sealed class Limiter
    {
        public float Tokens = BucketSize;
        public TimeSpan Refilled;
        public TimeSpan MutedUntil;
        public readonly Queue<TimeSpan> Recent = new();
    }

    private record struct Resolved(FSPingType Type, FSPingAudience Audience, string Label, EntityUid? Track,
        string? Icon = null, string? Build = null, float Lifetime = 6f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSPingRequestEvent>(OnRequest);
    }

    private void OnRequest(FSPingRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } sender
            || !HasComp<FSFriendlyFireComponent>(sender)
            || _mobState.IsIncapacitated(sender))
            return;

        if (!TryConsumeToken(args.SenderSession.UserId))
        {
            RaiseNetworkEvent(new FSPingRefusedEvent((float) WaitFor(args.SenderSession.UserId).TotalSeconds), args.SenderSession);
            return;
        }

        var coords = GetCoordinates(ev.Coords);
        if (!coords.IsValid(EntityManager) || !_transform.InRange(Transform(sender).Coordinates, coords, MaxRange))
            return;

        var target = GetEntity(ev.Target);
        if (target is { } t && (!Exists(t) || _container.IsEntityInContainer(t)))
            target = null;

        Ping(sender, coords, target, ev.Forced);
    }

    public FSPingType Ping(EntityUid sender, EntityCoordinates coords, EntityUid? target, FSPingType? forced)
    {
        var resolved = forced is { } kind ? ResolveForced(sender, kind, coords) : ResolveSmart(sender, coords, target);
        if (resolved.Type is FSPingType.NeedChems or FSPingType.Repair or FSPingType.BuildHere or FSPingType.Revive or FSPingType.Bay)
            resolved = resolved with { Lifetime = TaskPingLife };
        var id = Broadcast(sender, resolved, coords);

        switch (resolved.Type)
        {
            case FSPingType.Acknowledge:
                _tasks.Claim(sender, coords, target);
                break;
            case FSPingType.NeedChems:
                _medicPing.RequestChems(sender);
                _tasks.Open(resolved.Type, resolved.Audience, coords, sender, sender, resolved.Label, ping: id);
                break;
            case FSPingType.Repair:
            case FSPingType.BuildHere:
            case FSPingType.Revive:
            case FSPingType.Bay:
                _tasks.Open(resolved.Type, resolved.Audience, coords, resolved.Track, sender, resolved.Label, resolved.Build, ping: id);
                break;
        }

        return resolved.Type;
    }

    public bool TryConsumeToken(NetUserId user)
    {
        var now = _timing.RealTime;
        if (!_limits.TryGetValue(user, out var limit))
            _limits[user] = limit = new Limiter { Refilled = now };

        if (now < limit.MutedUntil)
            return false;

        var refilled = (float) ((now - limit.Refilled) / BucketRefill);
        limit.Tokens = MathF.Min(BucketSize, limit.Tokens + refilled);
        limit.Refilled = now;

        while (limit.Recent.Count > 0 && now - limit.Recent.Peek() > SpamWindow)
            limit.Recent.Dequeue();

        if (limit.Tokens < 1f)
            return false;

        limit.Tokens -= 1f;
        limit.Recent.Enqueue(now);
        if (limit.Recent.Count >= SpamLimit)
        {
            limit.MutedUntil = now + SpamMute;
            limit.Recent.Clear();
        }

        return true;
    }

    public TimeSpan WaitFor(NetUserId user)
    {
        if (!_limits.TryGetValue(user, out var limit))
            return TimeSpan.Zero;

        var now = _timing.RealTime;
        if (now < limit.MutedUntil)
            return limit.MutedUntil - now;

        return limit.Tokens >= 1f ? TimeSpan.Zero : BucketRefill * (1f - limit.Tokens);
    }

    private Resolved ResolveForced(EntityUid sender, FSPingType forced, EntityCoordinates coords)
    {
        switch (forced)
        {
            case FSPingType.NeedRepair:
                if (NearestDamaged(coords) is { } structure)
                    return RepairOf(structure);
                return new Resolved(FSPingType.NeedRepair, FSPingAudience.Engineering, Loc.GetString("fs-ping-need-repair"), null);
            case FSPingType.NeedChems:
                return new Resolved(FSPingType.NeedChems, FSPingAudience.Medical, Loc.GetString("fs-ping-need-chems"), sender, Lifetime: 10f);
            case FSPingType.Danger:
            case FSPingType.Go:
            case FSPingType.Defend:
            case FSPingType.FallBack:
            case FSPingType.NeedAmmo:
            case FSPingType.Acknowledge:
                return new Resolved(forced, FSPingAudience.Everyone, Loc.GetString("fs-ping-" + forced.ToString().ToLowerInvariant()), null);
            default:
                return new Resolved(FSPingType.Go, FSPingAudience.Everyone, Loc.GetString("fs-ping-go"), null);
        }
    }

    private Resolved ResolveSmart(EntityUid sender, EntityCoordinates coords, EntityUid? target)
    {
        if (target is { } t)
        {
            if (HasComp<FSGiantAbilitiesComponent>(t) || HasComp<FSRevenantComponent>(t))
                return new Resolved(FSPingType.Boss, FSPingAudience.Everyone, Loc.GetString("fs-ping-boss", ("name", Name(t))), t,
                    CompOrNull<FSEnemyIconComponent>(t)?.IconPath, Lifetime: 15f);

            if (HasComp<WaveSpawnedTagComponent>(t) && !_mobState.IsDead(t))
            {
                return HasComp<FSSpecialZombieComponent>(t)
                    ? new Resolved(FSPingType.Special, FSPingAudience.Everyone, Name(t), t, CompOrNull<FSEnemyIconComponent>(t)?.IconPath)
                    : new Resolved(FSPingType.Enemy, FSPingAudience.Everyone, Loc.GetString("fs-ping-enemy"), t, Lifetime: 4f);
            }

            if (t != sender && HasComp<FSFriendlyFireComponent>(t) && HasComp<MobStateComponent>(t) && _mobState.IsIncapacitated(t))
                return new Resolved(FSPingType.Revive, FSPingAudience.Medical, Loc.GetString("fs-ping-revive", ("name", Name(t))), t, Lifetime: 12f);

            if (_health.Fraction(t) is { } health && health < RepairThreshold)
                return RepairOf(t);

            if (TryComp<FSMechBayComponent>(t, out var bay) && !bay.Ready && bay.Next() is { } slot)
            {
                return new Resolved(FSPingType.Bay,
                    slot.Role == FSMechBayRole.Engineering ? FSPingAudience.Engineering : FSPingAudience.Science,
                    Loc.GetString("fs-ping-bay", ("stage", Loc.GetString(slot.Name)),
                        ("dept", Loc.GetString($"fs-mech-bay-role-{slot.Role.ToString().ToLowerInvariant()}"))), t, Lifetime: 10f);
            }

            if (TryComp<FSAmmoCrateComponent>(t, out var crate))
                return new Resolved(FSPingType.Ammo, FSPingAudience.Everyone, Loc.GetString("fs-ping-ammo-rounds", ("rounds", crate.Rounds)), t, Lifetime: 8f);

            if (TryComp<FSAmmoBoxComponent>(t, out var box))
                return new Resolved(FSPingType.Ammo, FSPingAudience.Everyone, Loc.GetString("fs-ping-ammo-uses", ("uses", box.UsesLeft)), t, Lifetime: 8f);

            if (HasComp<FSEmplacementComponent>(t))
                return new Resolved(FSPingType.Ammo, FSPingAudience.Everyone, Loc.GetString("fs-ping-ammo"), t, Lifetime: 8f);

            if (HasComp<DoorComponent>(t))
                return new Resolved(FSPingType.Door, FSPingAudience.Everyone, Loc.GetString("fs-ping-door"), t, Lifetime: 8f);

            if (HasComp<ItemComponent>(t))
                return new Resolved(FSPingType.Loot, FSPingAudience.Everyone, Name(t), t, Lifetime: 8f);
        }

        if (_hands.TryGetActiveItem(sender, out var held)
            && HasComp<FSEngineerOnlyRCDComponent>(held)
            && TryComp<RCDComponent>(held, out var rcd)
            && _proto.Index(rcd.ProtoId).Mode == RcdMode.ConstructObject)
        {
            var piece = _proto.Index(rcd.ProtoId);
            var name = piece.Prototype != null && _proto.TryIndex<EntityPrototype>(piece.Prototype, out var built) ? built.Name : piece.ID;
            return new Resolved(FSPingType.BuildHere, FSPingAudience.Engineering, Loc.GetString("fs-ping-build", ("name", name)), null,
                Build: piece.ID, Lifetime: 12f);
        }

        return new Resolved(FSPingType.Go, FSPingAudience.Everyone, Loc.GetString("fs-ping-go"), null);
    }

    private Resolved RepairOf(EntityUid structure)
    {
        var percent = (int) MathF.Round((_health.Fraction(structure) ?? 1f) * 100f);
        return new Resolved(FSPingType.Repair, FSPingAudience.Engineering,
            Loc.GetString("fs-ping-repair", ("name", Name(structure)), ("percent", percent)), structure, Lifetime: 10f);
    }

    private EntityUid? NearestDamaged(EntityCoordinates coords)
    {
        EntityUid? best = null;
        var bestDistance = NearbyRepairRange;
        var map = _transform.ToMapCoordinates(coords);
        var query = EntityQueryEnumerator<FSStructureHealthComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != map.MapId || _health.Fraction(uid) is not { } health || health >= RepairThreshold)
                continue;

            var distance = (_transform.GetWorldPosition(xform) - map.Position).Length();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = uid;
        }

        return best;
    }

    private int Broadcast(EntityUid sender, Resolved resolved, EntityCoordinates coords)
    {
        RaiseNetworkEvent(new FSPingEvent
        {
            Id = ++_nextId,
            Sender = GetNetEntity(sender),
            SenderName = Name(sender),
            Type = resolved.Type,
            Audience = resolved.Audience,
            Coords = GetNetCoordinates(coords),
            Target = resolved.Track is { } track ? GetNetEntity(track) : null,
            Label = resolved.Label,
            IconPath = resolved.Icon,
            BuildProto = resolved.Build,
            Lifetime = resolved.Lifetime,
        }, Filter.Broadcast());
        return _nextId;
    }
}
