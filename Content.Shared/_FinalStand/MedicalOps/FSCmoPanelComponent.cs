using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSCmoPanelComponent : Component
{
    [DataField, AutoNetworkedField]
    public FSMedicalDirective? ActiveDirective;

    [DataField, AutoNetworkedField]
    public TimeSpan McpReadyAt;

    [DataField, AutoNetworkedField]
    public TimeSpan MobilisationReadyAt;

    [DataField, AutoNetworkedField]
    public TimeSpan DirectiveReadyAt;
}

[Serializable, NetSerializable]
public enum FSCmoAbility : byte
{
    MassCasualtyProtocol,
    Mobilisation,
    DirectiveTrauma,
    DirectivePharma,
    DirectiveFieldOps,
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
