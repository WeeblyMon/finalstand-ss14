using System.Linq;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed partial class WaveGameRuleSystem
{
    [Dependency] private IVoteManager _votes = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private IVoteHandle? _difficultyVote;

    private void StartDifficultyVote(WaveGameRuleComponent comp)
    {
        CancelDifficultyVote();
        var seconds = _cfg.GetCVar(CCVars.FSDifficultyVoteSeconds);
        if (!comp.DifficultyVote || seconds <= 0)
            return;

        var options = new VoteOptions
        {
            Title = Loc.GetString("fs-difficulty-vote-title"),
            InitiatorText = Loc.GetString("fs-difficulty-vote-initiator"),
            Duration = TimeSpan.FromSeconds(seconds),
        };

        foreach (var difficulty in Enum.GetValues<FSDifficulty>())
        {
            options.Options.Add((Loc.GetString("fs-difficulty-vote-option",
                ("name", Loc.GetString(FSDifficultyRules.NameLocId(difficulty))),
                ("health", FormatPercent(FSDifficultyRules.HealthMultiplier(difficulty))),
                ("reward", FormatPercent(FSDifficultyRules.RewardMultiplier(difficulty)))), difficulty));
        }

        _difficultyVote = _votes.CreateVote(options);
        _difficultyVote.OnFinished += OnDifficultyVoteFinished;
    }

    private static string FormatPercent(float multiplier)
        => multiplier <= 1f ? "+0%" : $"+{MathF.Round((multiplier - 1f) * 100f)}%";

    private void OnDifficultyVoteFinished(IVoteHandle sender, VoteFinishedEventArgs args)
    {
        if (sender != _difficultyVote)
            return;
        _difficultyVote = null;

        var winners = args.Winners.OfType<FSDifficulty>().ToList();
        if (winners.Count == 0)
            return;

        if (!TrySetDifficulty(winners.Min()))
            _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-difficulty-vote-too-late"));
    }

    private void CancelDifficultyVote()
    {
        if (_difficultyVote == null)
            return;

        var vote = _difficultyVote;
        _difficultyVote = null;
        vote.OnFinished -= OnDifficultyVoteFinished;
        vote.Cancel();
    }

    public bool TrySetDifficulty(FSDifficulty difficulty)
    {
        if (!TryGetActiveRule(out _, out var comp, out _) || comp.DifficultyLocked || comp.WeeklyKey != null)
            return false;

        comp.Difficulty = difficulty;
        comp.EnemyTotalThisWave = CalcEnemyTotal(comp, CountScalingPlayers(), CountPrimarySpawners(comp));
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString("fs-difficulty-set", ("name", Loc.GetString(FSDifficultyRules.NameLocId(difficulty)))),
            Color.FromHex("#FF7043"));
        Log.Info($"[WaveGameRule] Difficulty set to {difficulty}.");
        return true;
    }

    public FSDifficulty GetDifficulty()
        => TryGetActiveRule(out _, out var comp, out _) ? comp.Difficulty : FSDifficulty.Normal;

    public float GetRewardMultiplier()
        => TryGetActiveRule(out _, out var comp, out _) ? RewardMultiplier(comp) : 1f;

    private float RewardMultiplier(WaveGameRuleComponent comp)
        => FSDifficultyRules.RewardMultiplier(comp.Difficulty) * FSRoundModifierRules.RewardMultiplier(GetModifiers(comp));

    private TimeSpan PrepDuration(WaveGameRuleComponent comp)
        => comp.PrepDuration * ModifierFactor(comp, m => m.PrepDuration);

    private void LockDifficulty(WaveGameRuleComponent comp)
    {
        if (comp.DifficultyLocked)
            return;

        comp.DifficultyLocked = true;
        CancelDifficultyVote();
        CancelRerollVote();
    }

    public readonly record struct RoundSummary(int WavesCleared, int Kills, bool Victory, FSDifficulty Difficulty,
        string? WeeklyKey = null, bool Assisted = false);

    public bool TryGetRoundSummary(out RoundSummary summary)
    {
        if (!TryGetActiveRule(out _, out var comp, out _))
        {
            summary = default;
            return false;
        }

        summary = new RoundSummary(comp.WavesCompleted, comp.TotalEnemiesKilled, comp.Victory, comp.Difficulty,
            comp.WeeklyKey, comp.AdminAssisted);
        return true;
    }
}
