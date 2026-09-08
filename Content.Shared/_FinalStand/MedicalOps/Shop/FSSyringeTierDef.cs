namespace Content.Shared._FinalStand.MedicalOps.Shop;

public sealed record FSSyringeTierDef(string Id, string Name, string Description, int Price, string SpawnId, int Rank);

public static class FSSyringeShopDefs
{
    public static readonly IReadOnlyList<FSSyringeTierDef> Tiers = new FSSyringeTierDef[]
    {
        new("mk2", "Syringe Gun Mk2", "8 rounds, faster cycling.", 6_000, "FSSyringeGunMk2", 1),
        new("mk3", "Syringe Gun Mk3", "10 rounds, rapid cycling.", 14_000, "FSSyringeGunMk3", 2),
    };

    public static FSSyringeTierDef? GetTier(string? id)
    {
        if (id == null)
            return null;

        foreach (var tier in Tiers)
        {
            if (tier.Id == id)
                return tier;
        }

        return null;
    }

    public static int RankOf(string? id) => GetTier(id)?.Rank ?? 0;
}
