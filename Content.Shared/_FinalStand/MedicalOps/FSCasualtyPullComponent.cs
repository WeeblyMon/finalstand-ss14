using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

// On the medic. Networked so the recovery panel can show its own cooldown without asking.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSCasualtyPullComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ReadyAt;
}
