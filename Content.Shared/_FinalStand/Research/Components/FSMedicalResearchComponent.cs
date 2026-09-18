using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

[RegisterComponent]
public sealed partial class FSMedicalResearchComponent : Component
{
    [DataField]
    public List<ProtoId<FSTechNodePrototype>> UnlockedNodes = new();

    [ViewVariables]
    public HashSet<string> UnlockedLookup = new();
}
