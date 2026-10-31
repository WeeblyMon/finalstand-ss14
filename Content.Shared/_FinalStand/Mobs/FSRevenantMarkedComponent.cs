using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSRevenantMarkedComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField] public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField] public EntityUid? MarkedByRevenant;
}
