// The rivet gun feeds itself a sheet at a time from the steel its holder is carrying.
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Repair;

public sealed class FSRivetGunSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    private const int SearchDepth = 2;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRivetGunComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<FSRivetGunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled
            || !TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo)
            || ammo.Count is not 0)
            return;

        var sheet = FindMaterial(args.User, ent.Comp.Material);
        if (sheet == null || !_stack.TryUse(sheet.Value.AsNullable(), 1))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("fs-rivet-gun-no-steel");
            return;
        }

        _gun.ChangeBasicEntityAmmoCount((ent, ammo), ent.Comp.RivetsPerSheet);
    }

    public int CountMaterial(EntityUid user, ProtoId<StackPrototype> material)
    {
        var count = 0;
        foreach (var stack in Stacks(user, material, 0))
            count += stack.Comp.Count;

        return count;
    }

    private Entity<StackComponent>? FindMaterial(EntityUid user, ProtoId<StackPrototype> material)
    {
        foreach (var stack in Stacks(user, material, 0))
            return stack;

        return null;
    }

    private IEnumerable<Entity<StackComponent>> Stacks(EntityUid root, ProtoId<StackPrototype> material, int depth)
    {
        if (!TryComp<ContainerManagerComponent>(root, out var manager))
            yield break;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (TryComp<StackComponent>(item, out var stack) && stack.StackTypeId == material && stack.Count > 0)
                    yield return (item, stack);

                if (depth >= SearchDepth)
                    continue;

                foreach (var nested in Stacks(item, material, depth + 1))
                    yield return nested;
            }
        }
    }
}
