// The CMO's command abilities. Every effect goes through FSMedicalBonusSystem so they stay capped.

using Content.Shared.Actions;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSMassCasualtyProtocolEvent : InstantActionEvent { }

public sealed partial class FSMedicalMobilisationEvent : InstantActionEvent { }

public sealed partial class FSMedicalDirectiveEvent : InstantActionEvent
{
    [DataField(required: true)]
    public FSMedicalDirective Directive;
}

public enum FSMedicalDirective : byte
{
    Trauma,
    Pharma,
    FieldOps,
}
