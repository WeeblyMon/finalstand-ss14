using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSZombieVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public int DamageStage;

    [DataField, AutoNetworkedField]
    public int DeathAlt;

    [DataField, AutoNetworkedField]
    public bool AltPicked;

    [DataField, AutoNetworkedField]
    public bool SimpleSpriteMode;
}
