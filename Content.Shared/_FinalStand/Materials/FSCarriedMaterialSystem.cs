using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Materials;

// Carried means anywhere on the player: hands, pockets, belt, bags inside bags.
public sealed partial class FSCarriedMaterialSystem : EntitySystem
{
    [Dependency] private SharedStackSystem _stack = default!;

    private readonly List<Entity<StackComponent>> _stacks = new();

    public int Count(EntityUid user, ProtoId<StackPrototype> material)
    {
        Collect(user, material);
        var total = 0;
        foreach (var stack in _stacks)
            total += stack.Comp.Count;

        return total;
    }

    public Dictionary<ProtoId<StackPrototype>, int> CountAll(EntityUid? user)
    {
        var counts = new Dictionary<ProtoId<StackPrototype>, int>();
        if (user is { } root)
            CountInto(root, counts);

        return counts;
    }

    public bool Has(EntityUid user, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        var carried = CountAll(user);
        foreach (var (material, need) in materials)
        {
            if (carried.GetValueOrDefault(material) < need)
                return false;
        }

        return true;
    }

    public bool TryConsume(EntityUid user, ProtoId<StackPrototype> material, int amount)
    {
        if (Count(user, material) < amount)
            return false;

        Spend(amount);
        return true;
    }

    public bool TryConsume(EntityUid user, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        if (!Has(user, materials))
            return false;

        foreach (var (material, need) in materials)
        {
            Collect(user, material);
            Spend(need);
        }

        return true;
    }

    private void Spend(int amount)
    {
        foreach (var stack in _stacks)
        {
            var take = Math.Min(amount, stack.Comp.Count);
            _stack.ReduceCount((stack.Owner, stack.Comp), take);
            amount -= take;
            if (amount == 0)
                return;
        }
    }

    private void Collect(EntityUid user, ProtoId<StackPrototype> material)
    {
        _stacks.Clear();
        CollectFrom(user, material);
    }

    private void CountInto(EntityUid root, Dictionary<ProtoId<StackPrototype>, int> counts)
    {
        if (!TryComp<ContainerManagerComponent>(root, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(contained, out var stack))
                    counts[stack.StackTypeId] = counts.GetValueOrDefault(stack.StackTypeId) + stack.Count;
                else
                    CountInto(contained, counts);
            }
        }
    }

    private void CollectFrom(EntityUid root, ProtoId<StackPrototype> material)
    {
        if (!TryComp<ContainerManagerComponent>(root, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(contained, out var stack))
                {
                    if (stack.StackTypeId == material && stack.Count > 0)
                        _stacks.Add((contained, stack));
                }
                else
                {
                    CollectFrom(contained, material);
                }
            }
        }
    }
}
