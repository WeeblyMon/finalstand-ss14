namespace Content.Server._FinalStand.MedicalOps;

public static class FSMedicalPayoutRates
{
    public const float SupplierRate = 0.3f;

    public const int SupplierCreditsPerPoint = 6;
    public const int SupplierFundPerPoint = 3;

    public const int ChemClaimBudget = 400;

    /// <summary>Per-crewmate budget when one throw buffs a whole group.</summary>
    public const int SplashClaimBudget = 120;
}
