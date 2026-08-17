// Tracks fuel for the chainsaw. Drained per swing, refilled from welder/fuel tanks.
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Chainsaw;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSChainsawFuelComponent : Component
{
    [DataField, AutoNetworkedField] public float CurrentFuel = 50f;
    [DataField, AutoNetworkedField] public float BaseMaxFuel = 50f;
    [DataField, AutoNetworkedField] public float MaxFuelMultiplier = 1f;
    [DataField] public float BaseFuelPerSwing = 1.0f;
    [DataField] public float FuelPerWelderUnit = 1.0f;

    public float MaxFuel => BaseMaxFuel * MaxFuelMultiplier;
}
