using System.Linq;
using Content.Server.Voting;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed partial class WaveGameRuleSystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    private IVoteHandle? _rerollVote;

    private void RollModifiers(WaveGameRuleComponent comp)
    {
        var count = _cfg.GetCVar(CCVars.FSRoundModifierCount);
        var pool = _proto.EnumeratePrototypes<FSRoundModifierPrototype>().OrderBy(p => p.ID).ToList();
        var rolled = FSRoundModifierRules.Roll(pool, Math.Max(0, count), () => RobustRandom.NextFloat());

        comp.Modifiers.Clear();
        comp.Modifiers.AddRange(rolled.Select(m => new ProtoId<FSRoundModifierPrototype>(m.ID)));
        comp.EnemyTotalThisWave = CalcEnemyTotal(comp, CountScalingPlayers(), CountPrimarySpawners(comp));
        AnnounceModifiers(comp);
    }

    private void AnnounceModifiers(WaveGameRuleComponent comp)
    {
        var modifiers = GetModifiers(comp).ToList();
        if (modifiers.Count == 0)
            return;

        foreach (var modifier in modifiers)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-modifier-announce",
                ("name", Loc.GetString(modifier.Name)),
                ("description", Loc.GetString(modifier.Description))), Color.FromHex("#CE93D8"));
        }

        _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-modifier-bonus",
            ("percent", MathF.Round((FSRoundModifierRules.RewardMultiplier(modifiers) - 1f) * 100f))),
            Color.FromHex("#CE93D8"));
    }

    public IEnumerable<FSRoundModifierPrototype> GetModifiers(WaveGameRuleComponent comp)
    {
        foreach (var id in comp.Modifiers)
        {
            if (_proto.TryIndex(id, out var modifier))
                yield return modifier;
        }
    }

    public IReadOnlyList<FSRoundModifierPrototype> GetActiveModifiers()
        => TryGetActiveRule(out _, out var comp, out _) ? GetModifiers(comp).ToList() : Array.Empty<FSRoundModifierPrototype>();

    public float ModifierFactor(WaveGameRuleComponent comp, Func<FSRoundModifierPrototype, float> factor)
        => FSRoundModifierRules.Product(GetModifiers(comp), factor);

    public bool TrySetModifiers(IReadOnlyList<ProtoId<FSRoundModifierPrototype>> ids)
    {
        if (!TryGetActiveRule(out _, out var comp, out _) || comp.DifficultyLocked || comp.WeeklyKey != null)
            return false;

        var modifiers = new List<FSRoundModifierPrototype>();
        foreach (var id in ids)
        {
            if (!_proto.TryIndex(id, out var modifier) || modifiers.Any(m => !FSRoundModifierRules.Compatible(m, modifier)))
                return false;
            modifiers.Add(modifier);
        }

        comp.Modifiers.Clear();
        comp.Modifiers.AddRange(ids);
        comp.EnemyTotalThisWave = CalcEnemyTotal(comp, CountScalingPlayers(), CountPrimarySpawners(comp));
        AnnounceModifiers(comp);
        return true;
    }

    public bool TryStartRerollVote(ICommonSession? initiator, out string error)
    {
        error = string.Empty;
        if (!TryGetActiveRule(out _, out var comp, out _))
        {
            error = Loc.GetString("fs-modifier-reroll-no-round");
            return false;
        }

        if (comp.DifficultyLocked)
        {
            error = Loc.GetString("fs-modifier-reroll-too-late");
            return false;
        }

        if (comp.ModifiersRerolled || _rerollVote != null)
        {
            error = Loc.GetString("fs-modifier-reroll-used");
            return false;
        }

        var options = new VoteOptions
        {
            Title = Loc.GetString("fs-modifier-reroll-title"),
            Duration = TimeSpan.FromSeconds(Math.Max(5, _cfg.GetCVar(CCVars.FSDifficultyVoteSeconds) / 2)),
            Options =
            {
                (Loc.GetString("fs-modifier-reroll-yes"), true),
                (Loc.GetString("fs-modifier-reroll-no"), false),
            },
        };
        options.SetInitiatorOrServer(initiator);

        _rerollVote = _votes.CreateVote(options);
        _rerollVote.OnFinished += OnRerollVoteFinished;
        _rerollVote.OnCancelled += OnRerollVoteCancelled;
        return true;
    }

    private void OnRerollVoteFinished(IVoteHandle sender, VoteFinishedEventArgs args)
    {
        if (sender != _rerollVote)
            return;
        _rerollVote = null;

        if (!TryGetActiveRule(out _, out var comp, out _) || comp.DifficultyLocked)
            return;

        comp.ModifiersRerolled = true;
        if (args.Winner is true)
            RollModifiers(comp);
    }

    private void OnRerollVoteCancelled(IVoteHandle sender)
    {
        if (sender == _rerollVote)
            _rerollVote = null;
    }

    private void CancelRerollVote()
    {
        if (_rerollVote == null)
            return;

        var vote = _rerollVote;
        _rerollVote = null;
        vote.OnFinished -= OnRerollVoteFinished;
        vote.OnCancelled -= OnRerollVoteCancelled;
        vote.Cancel();
    }

    public bool TryStartWeekly(FSWeeklyChallengePrototype challenge, string weekKey)
    {
        if (!TryGetActiveRule(out _, out var comp, out _) || comp.DifficultyLocked || comp.WeeklyKey != null)
            return false;

        if (!TrySetModifiers(challenge.Modifiers) || !TrySetDifficulty(challenge.Difficulty))
            return false;

        comp.WeeklyChallenge = challenge.ID;
        comp.WeeklyKey = weekKey;
        comp.ModifiersRerolled = true;
        CancelDifficultyVote();
        CancelRerollVote();
        _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-weekly-started",
            ("name", Loc.GetString(challenge.Name)), ("wave", challenge.ClearWave)), Color.FromHex("#FFB300"));
        return true;
    }

    public bool TryGetWeekly(out FSWeeklyChallengePrototype challenge, out string weekKey)
    {
        challenge = default!;
        weekKey = string.Empty;
        if (!TryGetActiveRule(out _, out var comp, out _) || comp.WeeklyChallenge is not { } id || comp.WeeklyKey is not { } key)
            return false;

        if (!_proto.TryIndex(id, out var proto))
            return false;

        challenge = proto;
        weekKey = key;
        return true;
    }

    public bool IsLocked() => TryGetActiveRule(out _, out var comp, out _) && comp.DifficultyLocked;
}
