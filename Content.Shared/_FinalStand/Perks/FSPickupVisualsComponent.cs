// Spins and bobs a ground pickup's sprite so it reads as collectible.
namespace Content.Shared._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSPickupVisualsComponent : Component
{
    [DataField]
    public float SpinDegreesPerSecond = 42f;

    [DataField]
    public float BobHeight = 0.06f;

    [DataField]
    public float BobSeconds = 1.6f;
}
