using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

// Steps a surgeon started and was interrupted on. Retrying one resumes at a discount instead of
// restarting from zero, which is what made BreakOnMove punishing rather than tense.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSurgeryProgressComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<string> Started = new();
}
