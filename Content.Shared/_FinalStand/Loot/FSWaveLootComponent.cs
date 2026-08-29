namespace Content.Shared._FinalStand.Loot;

// Only sheets dropped by this system carry it, so cleanup can never touch pre-mapped materials.
[RegisterComponent]
public sealed partial class FSWaveLootComponent : Component
{
    [DataField]
    public int DroppedOnWave;
}
