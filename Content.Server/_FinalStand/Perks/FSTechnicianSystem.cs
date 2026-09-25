// Technician perk: extra deployable stock and deploy cap for whoever carries or places the item.
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Perks;

public sealed class FSTechnicianSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    private const int MaxHolderDepth = 4;

    public int GetBonusForUser(EntityUid user, FSDeployableItemComponent item)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return 0;

        return FSPerkBonusConstants.TechnicianBonus(perks.GetSlottedLevel("Technician"), item.MaxStock);
    }

    public int GetBonusForItem(EntityUid item, FSDeployableItemComponent comp)
    {
        var current = Transform(item).ParentUid;
        for (var i = 0; i < MaxHolderDepth && current.IsValid(); i++)
        {
            if (_mind.TryGetMind(current, out _, out _))
                return GetBonusForUser(current, comp);

            current = Transform(current).ParentUid;
        }

        return 0;
    }
}
