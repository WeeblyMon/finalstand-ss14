using Content.Shared._FinalStand.Perks;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Smoking;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSSmokingKillsSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public bool IsSmoking(EntityUid body)
    {
        return _inventory.TryGetSlotEntity(body, "mask", out var worn)
            && TryComp<SmokableComponent>(worn, out var smokable)
            && smokable.State == SmokableState.Lit;
    }

    public float GetMultiplier(EntityUid body, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("SmokingKills");
        return level > 0 && IsSmoking(body) ? 1f + level * FSPerkBonusConstants.SmokingKillsPerLevel : 1f;
    }
}
