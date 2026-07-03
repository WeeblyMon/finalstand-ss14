using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._FinalStand.Grenades;
using Content.Shared.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Grenades;

/// <summary>
/// Server-side tracker attached to FSBaitDecoy so we can undo zombie-player ignores on cleanup.
/// </summary>
[RegisterComponent]
public sealed partial class FSBaitAttractTrackerComponent : Component
{
    // zombie uid → set of player uids we told that zombie to ignore
    public Dictionary<EntityUid, HashSet<EntityUid>> ZombieToIgnoredPlayers = new();
}

public sealed class FSBaitAttractSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float AttractRadius = 20f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBaitAttractTrackerComponent, ComponentShutdown>(OnTrackerShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSBaitDecoyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var tracker = EnsureComp<FSBaitAttractTrackerComponent>(uid);
            var worldPos = _transform.GetWorldPosition(uid);
            var coords = new MapCoordinates(worldPos, xform.MapID);

            var zombieCandidates = new HashSet<Entity<HTNComponent>>();
            _lookup.GetEntitiesInRange<HTNComponent>(coords, AttractRadius, zombieCandidates);

            var playerCandidates = new HashSet<Entity<ActorComponent>>();
            _lookup.GetEntitiesInRange<ActorComponent>(coords, AttractRadius, playerCandidates);

            foreach (var (npcUid, _) in zombieCandidates)
            {
                _npcFaction.AggroEntity(npcUid, uid);
                _npc.SetBlackboard(npcUid, "Target", uid);
                _npc.SetBlackboard(npcUid, "TargetCoordinates", new EntityCoordinates(uid, Vector2.Zero));
                if (!tracker.ZombieToIgnoredPlayers.TryGetValue(npcUid, out var ignoredSet))
                {
                    ignoredSet = new HashSet<EntityUid>();
                    tracker.ZombieToIgnoredPlayers[npcUid] = ignoredSet;
                }

                foreach (var (playerUid, _) in playerCandidates)
                {
                    if (!ignoredSet.Add(playerUid))
                        continue;
                    _npcFaction.IgnoreEntity(npcUid, playerUid);
                }
            }
        }
    }

    private void OnTrackerShutdown(Entity<FSBaitAttractTrackerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var (zombieUid, ignoredPlayers) in ent.Comp.ZombieToIgnoredPlayers)
        {
            foreach (var playerUid in ignoredPlayers)
                _npcFaction.DeIgnoreEntity(zombieUid, playerUid);
        }
    }
}
