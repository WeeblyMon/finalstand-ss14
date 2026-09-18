namespace Content.Shared._FinalStand.MedicalOps;

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
