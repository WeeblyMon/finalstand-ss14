using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSCmoPanelComponent : Component
{
    [DataField, AutoNetworkedField]
    public FSMedicalDirective? ActiveDirective;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan McpReadyAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan MobilisationReadyAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan DirectiveReadyAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan DividendReadyAt;

    [DataField, AutoNetworkedField]
    public bool DividendUnlocked;
}

[Serializable, NetSerializable]
public enum FSCmoAbility : byte
{
    MassCasualtyProtocol,
    Mobilisation,
    DirectiveTrauma,
    DirectivePharma,
    DirectiveFieldOps,
    Dividend,
}

[Serializable, NetSerializable]
public sealed class FSCmoAbilityRequestEvent : EntityEventArgs
{
    public FSCmoAbility Ability;

    public FSCmoAbilityRequestEvent(FSCmoAbility ability)
    {
        Ability = ability;
    }
}
