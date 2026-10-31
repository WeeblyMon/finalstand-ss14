using Content.Shared.Mech.Components;
using Content.Shared.Tag;

namespace Content.Shared._FinalStand.Mech;

// Per-mount-size weapon caps, on top of the mech's equipment whitelist.
public sealed partial class FSMechMountSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;

    public bool HasRoom(EntityUid mech, EntityUid item)
    {
        if (!TryComp<FSCombatMechComponent>(mech, out var combat) || !TryComp<MechComponent>(mech, out var comp))
            return true;

        foreach (var (mount, max) in combat.MountLimits)
        {
            if (!_tag.HasTag(item, mount))
                continue;

            var used = 0;
            foreach (var fitted in comp.EquipmentContainer.ContainedEntities)
            {
                if (_tag.HasTag(fitted, mount))
                    used++;
            }

            if (used >= max)
                return false;
        }

        return true;
    }
}
