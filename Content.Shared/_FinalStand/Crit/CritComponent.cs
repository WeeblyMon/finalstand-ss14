using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Crit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CritComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BaseCritChance = 0f;

    [DataField, AutoNetworkedField]
    public float CritMultiplier = FSCritMath.DefaultMultiplier;
}
