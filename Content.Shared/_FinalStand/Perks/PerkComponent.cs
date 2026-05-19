using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PerkComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<PerkType> ActivePerks = [];

    public bool HasPerk(PerkType perk) => ActivePerks.Contains(perk);

    public bool CanAddPerk() => ActivePerks.Count < 4;
}
