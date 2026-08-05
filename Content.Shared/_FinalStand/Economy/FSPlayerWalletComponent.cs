namespace Content.Shared._FinalStand.Economy;

[RegisterComponent]
public sealed partial class FSPlayerWalletComponent : Component
{
    public int Credits = 0;
    public int PerkPoints = 0;

    /// <summary>
    /// Set once the saved row has been read onto this mind. Other systems pay this player from
    /// the same spawn event, so the wallet can already exist before the load runs — this marks
    /// whether the load itself has happened, not whether the component exists.
    /// </summary>
    public bool Loaded = false;
}
