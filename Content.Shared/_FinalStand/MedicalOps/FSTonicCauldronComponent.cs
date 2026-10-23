namespace Content.Shared._FinalStand.MedicalOps;

// Brewed by a chemist during prep. Each crewmate drinks once for a small bonus that lasts until the wave ends.
[RegisterComponent]
public sealed partial class FSTonicCauldronComponent : Component
{
    public const string Source = FSApplyCombatBuff.SourcePrefix + "tonic";

    [DataField]
    public Dictionary<FSMedicalBonusCategory, float> Bonuses = new()
    {
        [FSMedicalBonusCategory.Damage] = 0.05f,
        [FSMedicalBonusCategory.Movement] = 0.05f,
    };

    [DataField]
    public LocId Name = "fs-tonic-name";

    [DataField]
    public int BrewerPay = 20;

    [DataField]
    public float CatalystPerDrinker = 5f;

    [ViewVariables]
    public EntityUid? Brewer;

    [ViewVariables]
    public HashSet<EntityUid> Drinkers = new();
}
