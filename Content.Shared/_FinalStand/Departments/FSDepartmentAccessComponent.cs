using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Departments;

// Replicated department membership, so shop and guidebook UI can read it off the player instead of being pushed it.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSDepartmentAccessComponent : Component
{
    [AutoNetworkedField]
    public bool Science;

    [AutoNetworkedField]
    public bool Engineering;
}
