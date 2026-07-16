using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSRadiationMarkComponent : Component
{
    [DataField, AutoNetworkedField] public float DamageMultiplier = 1.5f;
    [DataField] public TimeSpan ExpiresAt;

    [DataField] public bool HasDot;
    [DataField] public float DotPerSecond;
    [DataField] public float DotRemaining;
}
