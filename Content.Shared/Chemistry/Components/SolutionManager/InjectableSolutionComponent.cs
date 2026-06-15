using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components.SolutionManager;

/// <summary>
///     Denotes a solution which can be added with syringes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InjectableSolutionComponent : Component
{

    /// <summary>
    /// Solution name which can be added with syringes.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string Solution = "default";
}
