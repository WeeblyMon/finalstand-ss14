using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

// A chem cloud on the ground: every tick it touches crew with its support reagents and everything else with its hostile ones.
[RegisterComponent]
public sealed partial class FSChemZoneComponent : Component
{
    [DataField]
    public float Radius = 2.5f;

    [DataField]
    public float Lifetime = 6f;

    [DataField]
    public float TickInterval = 0.5f;

    [DataField]
    public EntProtoId CloudProto = "FSChemCloud";

    [ViewVariables]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    [ViewVariables]
    public EntityUid? Chemist;

    [ViewVariables]
    public EntityUid? Launcher;

    [ViewVariables]
    public float Scale = 1f;

    [ViewVariables]
    public bool Catalysed;

    [ViewVariables]
    public bool Chains;

    [ViewVariables]
    public string? Label;

    [ViewVariables]
    public float Accumulator;

    [ViewVariables]
    public HashSet<EntityUid> Caught = new();
}
