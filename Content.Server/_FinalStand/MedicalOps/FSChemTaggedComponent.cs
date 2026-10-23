namespace Content.Server._FinalStand.MedicalOps;

// Marks a wave enemy a chemist's cloud has worked on, so a teammate's kill can pay them an assist.
[RegisterComponent]
public sealed partial class FSChemTaggedComponent : Component
{
    [ViewVariables]
    public EntityUid Chemist;

    [ViewVariables]
    public EntityUid? Launcher;

    [ViewVariables]
    public TimeSpan Until;

    [ViewVariables]
    public float Damage;

    [ViewVariables]
    public bool Debuffed;
}
