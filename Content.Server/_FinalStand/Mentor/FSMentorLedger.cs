namespace Content.Server._FinalStand.Mentor;

public sealed class FSMentorLedger
{
    public const int RoundCap = 40;
    public const int PerRookieRoundCap = 10;
    public const int WeekCap = 150;

    private readonly Dictionary<Guid, int> _round = new();
    private readonly Dictionary<(Guid Mentor, Guid Rookie), int> _pairs = new();
    private readonly HashSet<(Guid Mentor, Guid Rookie)> _voided = new();

    public int Grant(Guid mentor, Guid rookie, int amount, int weekSoFar)
    {
        if (amount <= 0 || mentor == rookie || _voided.Contains((mentor, rookie)))
            return 0;

        var round = _round.GetValueOrDefault(mentor);
        var pair = _pairs.GetValueOrDefault((mentor, rookie));
        var allowed = Math.Min(amount, Math.Min(RoundCap - round, Math.Min(PerRookieRoundCap - pair, WeekCap - weekSoFar)));
        if (allowed <= 0)
            return 0;

        _round[mentor] = round + allowed;
        _pairs[(mentor, rookie)] = pair + allowed;
        return allowed;
    }

    public void Void(Guid mentor, Guid rookie) => _voided.Add((mentor, rookie));

    public bool IsVoided(Guid mentor, Guid rookie) => _voided.Contains((mentor, rookie));

    public int EarnedThisRound(Guid mentor) => _round.GetValueOrDefault(mentor);

    public void Reset()
    {
        _round.Clear();
        _pairs.Clear();
        _voided.Clear();
    }
}
