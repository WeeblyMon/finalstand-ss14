using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.NPC;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Grenades;

[RegisterComponent]
public sealed partial class FSBaitAttractTrackerComponent : Component
{
    // zombie uid → bait uid that zombie is currently chasing
    public Dictionary<EntityUid, EntityUid> ZombieToBait = new();
}

public sealed class FSBaitAttractSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

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
            if (_container.IsEntityInContainer(uid))
                continue;

            var tracker = EnsureComp<FSBaitAttractTrackerComponent>(uid);
            var worldPos = _transform.GetWorldPosition(uid);
            var coords = new MapCoordinates(worldPos, xform.MapID);

            var zombieCandidates = new HashSet<Entity<HTNComponent>>();
            _lookup.GetEntitiesInRange<HTNComponent>(coords, AttractRadius, zombieCandidates);

            foreach (var (npcUid, _) in zombieCandidates)
            {
                if (tracker.ZombieToBait.ContainsKey(npcUid))
                    continue;

                tracker.ZombieToBait[npcUid] = uid;
                _npc.SetBlackboard(npcUid, FSAIBlackboardKeys.BaitTarget, uid);
            }
        }
    }

    private void OnTrackerShutdown(Entity<FSBaitAttractTrackerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var (zombieUid, _) in ent.Comp.ZombieToBait)
        {
            if (!TryComp<HTNComponent>(zombieUid, out var htn))
                continue;
            htn.Blackboard.Remove<EntityUid>(FSAIBlackboardKeys.BaitTarget);
        }
    }
}
