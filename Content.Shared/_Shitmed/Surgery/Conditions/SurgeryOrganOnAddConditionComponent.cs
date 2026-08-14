//

using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

// What components are necessary in the child organs' OnAdd fields for the surgery to be valid.
// At least one component matching (or missing, for Inverse) makes the surgery valid.
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryOrganOnAddConditionComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<OrganCategoryPrototype>, ComponentRegistry> Components = new();

    [DataField]
    public bool Inverse;
}
