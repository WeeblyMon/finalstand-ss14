using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSRadiationMarkComponent : Component
{
    [DataField, AutoNetworkedField] public float DamageMultiplier = 1.5f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField] public bool HasDot;
    [DataField] public float DotPerSecond;
    [DataField] public float DotRemaining;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDot;

    // Whoever fired the marking shot, so the DoT's kills pay them.
    [ViewVariables]
    public EntityUid? Origin;
}
