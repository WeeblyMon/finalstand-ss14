using System.Numerics;
using Content.Shared._FinalStand.Medical;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class InitialBodySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;

    private EntityQuery<ChildOrganComponent> _childQuery;

    public override void Initialize()
    {
        base.Initialize();

        _childQuery = GetEntityQuery<ChildOrganComponent>();

        SubscribeLocalEvent<InitialBodyComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InitialBodyComponent, RejuvenateEvent>(OnRejuvenate);
    }

    // FINALSTAND: rejuvenate healed wounds but left amputated limbs missing.
    private void OnRejuvenate(Entity<InitialBodyComponent> ent, ref RejuvenateEvent args)
    {
        if (!_net.IsServer
            || TerminatingOrDeleted(ent)
            || !TryComp<ContainerManagerComponent>(ent, out var containerComp)
            || !_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container, containerComp))
        {
            return;
        }

        var present = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        foreach (var organ in _lookup.GetBodyOrgans(ent.Owner))
        {
            if (organ.Comp.Category is { } category)
                present[category] = organ.Owner;
        }

        var xform = Transform(ent);
        var coords = new EntityCoordinates(ent, Vector2.Zero);
        var restored = new HashSet<ProtoId<OrganCategoryPrototype>>();

        foreach (var (part, proto) in ent.Comp.Organs)
        {
            if (present.ContainsKey(part))
                continue;

            var spawn = Spawn(proto, coords);
            if (!_container.Insert(spawn, container, containerXform: xform))
            {
                Del(spawn);
                continue;
            }

            present[part] = spawn;
            restored.Add(part);
        }

        if (restored.Count == 0 || ent.Comp.Relationships is null)
            return;

        foreach (var (parentId, children) in ent.Comp.Relationships)
        {
            if (!present.TryGetValue(parentId, out var parentUid))
                continue;

            foreach (var childId in children)
            {
                if (!restored.Contains(parentId) && !restored.Contains(childId))
                    continue;

                if (present.TryGetValue(childId, out var childUid)
                    && _childQuery.TryComp(childUid, out var childComp)
                    && childComp.Parent == null)
                {
                    _organRelation.Relate(parentUid, (childUid, childComp));
                }
            }
        }
    }

    private void OnMapInit(Entity<InitialBodyComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ContainerManagerComponent>(ent, out var containerComp))
            return;

        if (TerminatingOrDeleted(ent) || !Exists(ent))
            return;

        if (!_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container, containerComp))
        {
            Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(InitialBodyComponent)} is missing a container ({BodyComponent.ContainerID}).");
            return;
        }

        var xform = Transform(ent);
        var coords = new EntityCoordinates(ent, Vector2.Zero);
        var spawned = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();

        foreach (var (part, proto) in ent.Comp.Organs)
        {
            // TODO: When e#6192 is merged replace this all with TrySpawnInContainer...
            var spawn = Spawn(proto, coords);

            if (!_container.Insert(spawn, container, containerXform: xform))
            {
                Log.Error($"Entity {ToPrettyString(ent)} with a {nameof(InitialBodyComponent)} failed to insert an entity: {ToPrettyString(spawn)}.\n");
                Del(spawn);
                continue;
            }

            spawned[part] = spawn;
        }

        if (ent.Comp.Relationships is null)
            return;

        foreach (var (partId, parentUid) in spawned)
        {
            if (!ent.Comp.Relationships.TryGetValue(partId, out var children))
                continue;

            foreach (var childId in children)
            {
                if (!spawned.TryGetValue(childId, out var childUid))
                    continue;

                _organRelation.Relate(parentUid, childUid);
            }
        }
    }
}
