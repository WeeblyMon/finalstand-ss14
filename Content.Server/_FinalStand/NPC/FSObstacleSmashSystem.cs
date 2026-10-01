// Wave zombies attack loose furniture they are pushing against. Lockers and crates are not anchored, so the
// pathfinder, steering smash and breach targeting all ignore them and the zombie just walks into them forever.
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server.Destructible;
using Content.Server.NPC.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed partial class FSObstacleSmashSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    private const float TickInterval = 0.25f;
    private const float BlockedSeconds = 0.75f;
    private const float StalledSpeedSquared = 0.25f;
    private const float ReachBeyondRadius = 0.45f;

    private float _accumulator;
    private readonly Dictionary<EntityUid, float> _blockedFor = new();
    private readonly List<EntityUid> _stale = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;
        _accumulator -= TickInterval;

        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, NPCSteeringComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var steering, out var body))
        {
            var wantsToMove = steering.LastSteerDirection.LengthSquared() > 0.01f;
            if (!wantsToMove || body.LinearVelocity.LengthSquared() > StalledSpeedSquared)
            {
                _blockedFor.Remove(uid);
                continue;
            }

            var blocked = _blockedFor.GetValueOrDefault(uid) + TickInterval;
            _blockedFor[uid] = blocked;
            if (blocked < BlockedSeconds)
                continue;

            if (TryFindObstacle(uid, steering, out var obstacle))
                Smash(uid, obstacle);
        }

        foreach (var uid in _blockedFor.Keys)
        {
            if (!Exists(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
            _blockedFor.Remove(uid);
        _stale.Clear();
    }

    private bool TryFindObstacle(EntityUid uid, NPCSteeringComponent steering, out EntityUid obstacle)
    {
        obstacle = EntityUid.Invalid;
        var (ourLayer, ourMask) = _physics.GetHardCollision(uid);
        var pos = _transform.GetMapCoordinates(uid);
        var direction = steering.LastSteerDirection.Normalized();

        var best = -1f;
        foreach (var candidate in _lookup.GetEntitiesInRange(pos, steering.Radius + ReachBeyondRadius, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (candidate == uid || !IsSmashable(candidate, ourLayer, ourMask))
                continue;

            var toCandidate = _transform.GetWorldPosition(candidate) - pos.Position;
            var facing = toCandidate.LengthSquared() > 0.0001f ? Vector2.Dot(direction, toCandidate.Normalized()) : 1f;
            if (facing <= 0f || facing <= best)
                continue;

            best = facing;
            obstacle = candidate;
        }

        return obstacle.IsValid();
    }

    private bool IsSmashable(EntityUid candidate, int ourLayer, int ourMask)
    {
        if (HasComp<WaveSpawnedTagComponent>(candidate)
            || HasComp<MobStateComponent>(candidate)
            || HasComp<ItemComponent>(candidate)
            || HasComp<DoorComponent>(candidate)
            || !HasComp<DamageableComponent>(candidate)
            || !HasComp<DestructibleComponent>(candidate))
            return false;

        if (!TryComp<PhysicsComponent>(candidate, out var body) || !body.Hard || !body.CanCollide)
            return false;

        return (body.CollisionLayer & ourMask) != 0 || (body.CollisionMask & ourLayer) != 0;
    }

    private void Smash(EntityUid uid, EntityUid obstacle)
    {
        if (!_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) || weapon.NextAttack > _timing.CurTime)
            return;

        if (!TryComp<CombatModeComponent>(uid, out var combat))
            return;

        _combat.SetInCombatMode(uid, true, combat);
        _melee.AttemptLightAttack(uid, weaponUid, weapon, obstacle);
        _combat.SetInCombatMode(uid, false, combat);
    }
}
