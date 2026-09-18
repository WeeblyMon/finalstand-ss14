using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Deployables;

// deployed resupply crate with a shared pool of uses and an owner-only private mode
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSAmmoBoxComponent : Component
{
    [DataField, AutoNetworkedField]
    public int UsesLeft = 2;

    [DataField, AutoNetworkedField]
    public int MaxUses = 2;

    [DataField, AutoNetworkedField]
    public bool Private;

    [DataField]
    public TimeSpan RefillDuration = TimeSpan.FromSeconds(3);

}
