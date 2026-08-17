namespace Content.Shared._FinalStand.Economy;

[RegisterComponent]
public sealed partial class FSPlayerWalletComponent : Component
{
    public int Credits = 0;
    public int PerkPoints = 0;

    // Marks whether the saved row has been read, not whether the component exists — other systems
    // can pay this player from the same spawn event before the load runs, so the wallet may predate it.
    public bool Loaded = false;
}
