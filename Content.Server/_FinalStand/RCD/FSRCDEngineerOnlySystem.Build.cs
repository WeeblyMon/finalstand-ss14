using Content.Server._FinalStand.Deployables;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Placement;
using Content.Shared._FinalStand.RCD;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem
{
    [Dependency] private FSPlacementRuleSystem _placementRules = default!;
    [Dependency] private FSDeployableSystem _deployable = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private const float RefundFraction = 0.5f;

    private readonly List<Entity<StackComponent>> _stacks = new();

    private bool IsRecipeBlocked(EntityUid rcd, FSEngineerOnlyRCDComponent comp, AfterInteractEvent args)
    {
        if (!TryComp<RCDComponent>(rcd, out var rcdComp))
            return false;

        var rcdProto = _proto.Index(rcdComp.ProtoId);
        if (rcdProto.Mode != RcdMode.ConstructObject
            || !_proto.TryIndex<FSBuildRecipePrototype>(rcdComp.ProtoId.Id, out var recipe))
            return false;

        var user = args.User;
        Direction? facing = rcdProto.Rotation == RcdRotation.User ? rcdComp.ConstructionDirection : null;

        if (!_placementRules.IsTileBuildable(args.ClickLocation, blocksPath: true))
            return Refuse(rcd, user, Loc.GetString("fs-rcd-in-the-way"));

        if (_deployable.IsOccupied(args.ClickLocation, facing))
            return Refuse(rcd, user, Loc.GetString("fs-deployable-occupied"));

        if (recipe.CapGroup is { } group && comp.Caps.TryGetValue(group, out var max))
        {
            var used = CapUsed(user, group);
            if (used >= max)
                return Refuse(rcd, user, Loc.GetString("fs-rcd-cap",
                    ("group", Loc.GetString("fs-rcd-cap-group-" + group)), ("used", used), ("max", max)));
        }

        var missing = new List<string>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = CountMaterial(user, material);
            if (have < need)
                missing.Add(Loc.GetString("fs-rcd-need-entry", ("need", need), ("have", have),
                    ("name", Loc.GetString(_proto.Index(material).Name))));
        }

        return missing.Count > 0
            && Refuse(rcd, user, Loc.GetString("fs-rcd-need-materials", ("list", string.Join(", ", missing))));
    }

    private bool Refuse(EntityUid rcd, EntityUid user, string message)
    {
        _popup.PopupEntity(message, rcd, user, PopupType.Medium);
        return true;
    }

    private void OnBuilt(Entity<FSEngineerOnlyRCDComponent> ent, ref FSRCDBuiltEvent args)
    {
        var protoId = MetaData(args.Built).EntityPrototype?.ID;
        if (protoId == null || !_proto.TryIndex<FSBuildRecipePrototype>(protoId, out var recipe))
            return;

        if (!TryConsume(args.User, recipe))
        {
            QueueDel(args.Built);
            _popup.PopupEntity(Loc.GetString("fs-rcd-materials-gone"), ent, args.User, PopupType.Medium);
            return;
        }

        var directional = TryComp<RCDComponent>(ent, out var rcdComp)
            && _proto.Index(rcdComp.ProtoId).Rotation == RcdRotation.User;

        if (!Transform(args.Built).Anchored)
            _transform.AnchorEntity(args.Built);

        var deployedBy = EnsureComp<FSDeployedByComponent>(args.Built);
        deployedBy.OwnerMind = _mind.TryGetMind(args.User, out var mindId, out _) ? mindId : null;
        deployedBy.DeployedBy = args.User;
        deployedBy.SourceProto = protoId;
        deployedBy.Facing = directional ? args.Direction : null;
    }

    private void RefundDeconstruct(EntityUid target)
    {
        var protoId = MetaData(target).EntityPrototype?.ID;
        if (protoId == null || !_proto.TryIndex<FSBuildRecipePrototype>(protoId, out var recipe))
            return;

        var health = 1f;
        if (_destructible.TryGetDestroyedAt(target, out var destroyedAt) && destroyedAt > 0
            && TryComp<Content.Shared.Damage.Components.DamageableComponent>(target, out var damageable))
            health = Math.Clamp(1f - (damageable.TotalDamage / destroyedAt.Value).Float(), 0f, 1f);

        var coords = Transform(target).Coordinates;
        foreach (var (material, count) in recipe.Materials)
        {
            var amount = (int) MathF.Floor(count * health * RefundFraction);
            if (amount > 0)
                _stack.SpawnMultipleAtPosition(_proto.Index(material).Spawn, amount, coords);
        }
    }

    private int CapUsed(EntityUid user, string group)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return 0;

        var count = 0;
        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (deployedBy.OwnerMind == mindId
                && !TerminatingOrDeleted(uid)
                && _proto.TryIndex<FSBuildRecipePrototype>(deployedBy.SourceProto.Id, out var recipe)
                && recipe.CapGroup == group)
                count++;
        }

        return count;
    }

    private int CountMaterial(EntityUid user, string material)
    {
        _stacks.Clear();
        CollectStacks(user, material);
        var total = 0;
        foreach (var stack in _stacks)
            total += stack.Comp.Count;
        return total;
    }

    private bool TryConsume(EntityUid user, FSBuildRecipePrototype recipe)
    {
        foreach (var (material, need) in recipe.Materials)
        {
            if (CountMaterial(user, material) < need)
                return false;
        }

        foreach (var (material, need) in recipe.Materials)
        {
            _stacks.Clear();
            CollectStacks(user, material);
            var remaining = need;
            foreach (var stack in _stacks)
            {
                var take = Math.Min(remaining, stack.Comp.Count);
                _stack.ReduceCount((stack.Owner, stack.Comp), take);
                remaining -= take;
                if (remaining == 0)
                    break;
            }
        }

        return true;
    }

    private void CollectStacks(EntityUid root, string material)
    {
        if (!TryComp<ContainerManagerComponent>(root, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(contained, out var stack) && stack.StackTypeId == material)
                    _stacks.Add((contained, stack));
                else
                    CollectStacks(contained, material);
            }
        }
    }
}
