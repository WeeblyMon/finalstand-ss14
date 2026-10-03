using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Fortifications;

public enum FSUpgradeBlock : byte
{
    None,
    Owned,
    NeedsPrevious,
    BranchTaken,
}

public abstract class SharedFSUpgradeSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager Proto = default!;

    public FSUpgradeBlock Check(FSUpgradeableComponent comp, FSUpgradePrototype upgrade)
    {
        if (upgrade.Slot == FSUpgradeSlot.Branch)
        {
            if (comp.Branch == upgrade.ID)
                return FSUpgradeBlock.Owned;

            return comp.Branch == null ? FSUpgradeBlock.None : FSUpgradeBlock.BranchTaken;
        }

        if (HasReached(comp.Grade, upgrade.ID))
            return FSUpgradeBlock.Owned;

        return upgrade.Requires == null || upgrade.Requires == comp.Grade
            ? FSUpgradeBlock.None
            : FSUpgradeBlock.NeedsPrevious;
    }

    private bool HasReached(ProtoId<FSUpgradePrototype> current, ProtoId<FSUpgradePrototype> target)
    {
        ProtoId<FSUpgradePrototype>? step = current;
        while (step is { } id)
        {
            if (id == target)
                return true;

            step = Proto.Index(id).Requires;
        }

        return false;
    }
}
