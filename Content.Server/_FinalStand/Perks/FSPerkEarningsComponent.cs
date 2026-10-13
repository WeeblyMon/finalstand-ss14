namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSPerkEarningsComponent : Component
{
    public readonly Dictionary<string, FSPerkEarningsTotals> Entries = new();
}

public sealed class FSPerkEarningsTotals
{
    public int WaveCredits;
    public int GameCredits;
    public int WaveResearch;
    public int GameResearch;
}
