using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.Teleportation;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSExpiringPortalComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public TimeSpan? PausedLeft;

    [DataField, AutoNetworkedField]
    public float Lifetime;
}
