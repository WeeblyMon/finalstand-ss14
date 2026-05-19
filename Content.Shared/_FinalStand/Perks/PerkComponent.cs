using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PerkComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<PerkType> ActivePerks = [];

    // TODO(finalstand): set this when crit-granting perks are added.
    [DataField, AutoNetworkedField]
    public float PerkCritChanceContribution = 0f;

    public bool HasPerk(PerkType perk) => ActivePerks.Contains(perk);

    public bool CanAddPerk() => ActivePerks.Count < 4;
}
