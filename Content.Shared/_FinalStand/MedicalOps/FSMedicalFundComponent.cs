namespace Content.Shared._FinalStand.MedicalOps;

// Round-scoped department budget, earned by medical work and spent on supplies and research.
[RegisterComponent]
public sealed partial class FSMedicalFundComponent : Component
{
    [ViewVariables]
    public int Balance;

    [ViewVariables]
    public int LifetimeEarned;

    [ViewVariables]
    public Dictionary<EntityUid, int> ContributionByMind = new();
}
