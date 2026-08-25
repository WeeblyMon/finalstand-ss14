using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSRevenantMarkedComponent : Component
{
    [DataField, AutoNetworkedField] public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField] public EntityUid? MarkedByRevenant;
}
