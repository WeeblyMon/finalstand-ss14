namespace Content.Server._FinalStand.Research;

// Per-weapon bookkeeping so FSResearchStaticGrantSystem only ever applies the delta.
[RegisterComponent]
public sealed partial class FSResearchAppliedComponent : Component
{
    public Dictionary<string, int> AppliedLevels = new();
}
