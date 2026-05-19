using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Crit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CritComponent : Component
{
    /// base crit chance contributed by this weapon. Aggregated multiplicatively with perk/gear sources.
    [DataField, AutoNetworkedField]
    public float BaseCritChance = 0f;

    ///damage multiplier applied on a successful crit roll.
    [DataField, AutoNetworkedField]
    public float CritMultiplier = 1.5f;
}
