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

// Lockers and crates aren't anchored, so pathing and breach targeting ignore them and zombies would push into them forever.
// Anchored structures are left to breach targeting, which knows not to open walls onto space.
public sealed partial class FSObstacleSmashSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    private const float BlockedSeconds = 0.75f;
    private const float StalledSpeedSquared = 0.25f;

    // A slice of the horde per tick, so a stalled crowd doesn't do all its checks at once.
    private const int TickBuckets = 8;
    private int _tick;

    private readonly Dictionary<EntityUid, float> _blockedFor = new();
    private readonly List<EntityUid> _stale = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var bucket = _tick++ % TickBuckets;
        var elapsed = frameTime * TickBuckets;

        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, NPCSteeringComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var steering, out var body))
        {
            if (uid.Id % TickBuckets != bucket)
                continue;

            var wantsToMove = steering.LastSteerDirection.LengthSquared() > 0.01f;
            if (!wantsToMove || body.LinearVelocity.LengthSquared() > StalledSpeedSquared)
            {
                _blockedFor.Remove(uid);
                continue;
            }

            var blocked = _blockedFor.GetValueOrDefault(uid) + elapsed;
            _blockedFor[uid] = blocked;
            if (blocked < BlockedSeconds)
                continue;

            if (TryFindObstacle(uid, steering.LastSteerDirection, out var obstacle))
                Smash(uid, obstacle);
        }

        if (bucket != 0)
            return;

        foreach (var uid in _blockedFor.Keys)
        {
            if (!Exists(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
            _blockedFor.Remove(uid);
        _stale.Clear();
    }

    // Whatever is holding a stalled zombie back is touching it, so its own contacts are enough. A range lookup
    // per stalled zombie mostly turned up the rest of the crowd.
    internal bool TryFindObstacle(EntityUid uid, Vector2 heading, out EntityUid obstacle)
    {
        obstacle = EntityUid.Invalid;
        var (ourLayer, ourMask) = _physics.GetHardCollision(uid);
        var pos = _transform.GetWorldPosition(uid);
        var direction = heading.Normalized();

        var best = -1f;
        var contacts = _physics.GetContacts(uid);
        while (contacts.MoveNext(out var contact))
        {
            if (!contact.IsTouching || !contact.Hard)
                continue;

            var candidate = contact.OtherEnt(uid);
            if (!IsSmashable(candidate, ourLayer, ourMask))
                continue;

            var toCandidate = _transform.GetWorldPosition(candidate) - pos;
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
        if (Transform(candidate).Anchored
            || HasComp<WaveSpawnedTagComponent>(candidate)
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
