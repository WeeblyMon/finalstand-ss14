namespace Content.Server._FinalStand.MedicalOps;

public static class FSMedicalPayoutRates
{
    public const float SupplierRate = 0.3f;

    public const int SupplierCreditsPerPoint = 6;
    public const int SupplierFundPerPoint = 3;

    public const int ChemClaimBudget = 400;

    public const int SplashClaimBudget = 120;

    public const int CreditsPerHealPoint = 14;
    public const int FundPerHealPoint = 7;

    public const float HealFullBudget = 60f;
    public const float HealHalfBudget = 150f;
    public const float HealHalfRate = 0.35f;
    public const float MinPreHealDamage = 10f;

    public static readonly FSMedicalLine Stabilise = new(25, 225, 135);
    public static readonly FSMedicalLine Revive = new(60, 450, 270);
    public static readonly FSMedicalLine PatientSaved = new(0, 360, 270);
    public static readonly FSMedicalLine Topical = new(4, 60, 30);
    public static readonly FSMedicalLine MinorCure = new(4, 50, 30);
    public static readonly FSMedicalLine MajorCure = new(20, 150, 120);

    public const float AutomaticMultiplier = 0.5f;

    public const float PriorityMultiplier = 1.25f;

    public const int TopicalsPaidPerPatientPerWave = 6;
    public const int CuresPaidPerPatientPerWave = 4;
    public const int MassClearPatientsPaid = 4;
    public static readonly TimeSpan MassClearWindow = TimeSpan.FromSeconds(3);

    public static readonly IReadOnlyDictionary<string, float> JobMultipliers = new Dictionary<string, float>
    {
        ["ChiefMedicalOfficer"] = 1f,
        ["MedicalDoctor"] = 1f,
        ["CombatMedic"] = 1f,
        ["Chemist"] = 1f,
    };
}

public readonly record struct FSMedicalLine(int Points, int Credits, int Fund)
{
    public FSMedicalLine Scale(float credits, float rest)
    {
        return new FSMedicalLine((int) MathF.Round(Points * rest), (int) MathF.Round(Credits * credits), (int) MathF.Round(Fund * rest));
    }

    public FSMedicalLine Above(FSMedicalLine paid)
    {
        return new FSMedicalLine(Math.Max(0, Points - paid.Points), Math.Max(0, Credits - paid.Credits), Math.Max(0, Fund - paid.Fund));
    }

    public FSMedicalLine Max(FSMedicalLine other)
    {
        return new FSMedicalLine(Math.Max(Points, other.Points), Math.Max(Credits, other.Credits), Math.Max(Fund, other.Fund));
    }

    public bool IsEmpty => Points <= 0 && Credits <= 0 && Fund <= 0;
}
