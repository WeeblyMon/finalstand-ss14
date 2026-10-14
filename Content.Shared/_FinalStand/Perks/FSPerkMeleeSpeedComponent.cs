using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Perks;

/// <summary>
/// Melee attack speed from perks, networked so the client predicts the same swing rate.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSPerkMeleeSpeedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
