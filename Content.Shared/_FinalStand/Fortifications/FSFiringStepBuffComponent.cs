using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Fortifications;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSFiringStepBuffComponent : Component
{
    [AutoNetworkedField]
    public float Spread = 1f;

    [AutoNetworkedField]
    public float MeleeDamage = 1f;

    public EntityUid? Gun;
}
