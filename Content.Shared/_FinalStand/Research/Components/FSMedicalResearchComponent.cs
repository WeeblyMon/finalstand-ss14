using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

// Medical's half of research. Buying is atomic, so unlike the science station there is no progress,
// no queue and no per-player picks - just what has been paid for.
[RegisterComponent]
public sealed partial class FSMedicalResearchComponent : Component
{
    [DataField]
    public List<ProtoId<FSTechNodePrototype>> UnlockedNodes = new();

    [ViewVariables]
    public HashSet<string> UnlockedLookup = new();
}
