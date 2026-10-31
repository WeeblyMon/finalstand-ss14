using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Melee;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Economy;

public sealed partial class FSWaveRewardsSystem : EntitySystem
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private FSEnemyDamageTrackerSystem _damageTracker = default!;
    [Dependency] private FSArchetypeSystem _archetypes = default!;
    [Dependency] private FSTitleBuffSystem _titleBuffs = default!;
    [Dependency] private FSStyleSystem _style = default!;

    private readonly List<EntityUid> _assisters = new();

    private static readonly ProtoId<DepartmentPrototype> SecurityDepartment = "Security";
    private static readonly Color PrepSkipColor = Color.FromHex("#44BB44");

    // Kill value multiplier by wave, applied up to and including the listed wave.
    private static readonly (int UpToWave, float Multiplier)[] KillMultipliers =
    [
        (5, 1f),
        (10, 1.5f),
        (15, 2f),
        (20, 2.75f),
    ];

    private const float LateKillMultiplier = 3.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnEnemyDied);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<WavePrepSkippedEvent>(OnPrepSkipped);
        // After leveling: the catch-up is earned money, and the scoreboard only counts it once the mind has a level.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(FSLevelingSystem)]);
    }

    private bool TryGetRewards(out FSWaveRewardsComponent rewards)
    {
        var query = EntityQueryEnumerator<FSWaveRewardsComponent, ActiveGameRuleComponent>();
        if (query.MoveNext(out rewards!, out _))
            return true;

        return false;
    }

    private void OnEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (!TryGetRewards(out var rewards))
            return;

        var killCredits = KillCredits(args.Enemy, rewards);

        EntityUid? killerMind = null;
        if (args.Killer is { } killer && _mind.TryGetMind(killer, out var mindId, out _))
        {
            killerMind = mindId;
            // Style, archetype and title bonuses are the killer's own; assists are a share of the plain value.
            _wallet.GiveCredits(mindId, (int) (killCredits * _style.KillMoneyMultiplier(killer, args.Enemy)
                * _archetypes.Bonus(mindId).Credits * _titleBuffs.ForMind(mindId).KillCredits));

            if (IsSecurity(mindId))
                _wallet.GiveCredits(mindId, rewards.SecurityKillBonus);
        }

        if (!TryComp<FSEnemyDamageTrackerComponent>(args.Enemy, out var tracker))
            return;

        var assistCredits = (int) (killCredits * rewards.AssistShare);
        _damageTracker.GetAssisters(tracker, args.Killer, _assisters);
        foreach (var assister in _assisters)
        {
            if (_mind.TryGetMind(assister, out var assistMind, out _) && assistMind != killerMind)
                _wallet.GiveCredits(assistMind, assistCredits);
        }
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        if (!TryGetRewards(out var rewards))
            return;

        PayCrew(rewards, rewards.ClearBase + rewards.ClearPerWave * args.WaveNumber);
    }

    private void OnPrepSkipped(ref WavePrepSkippedEvent args)
    {
        if (!TryGetRewards(out var rewards))
            return;

        var bonus = (int) (args.Skipped.TotalSeconds * rewards.CreditsPerPrepSecondSkipped);
        if (bonus <= 0)
            return;

        PayCrew(rewards, bonus);
        _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-prep-skip-bonus", ("credits", bonus)), PrepSkipColor);
    }

    // Late joiners get what they'd have earned from crew-wide payouts, plus a flat amount per missed wave.
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!TryGetRewards(out var rewards)
            || !_mind.TryGetMind(ev.Mob, out var mindId, out _)
            || !rewards.CaughtUpUsers.Add(ev.Player.UserId))
        {
            return;
        }

        var wavesMissed = _waveRule.GetWaveNumber() - 1;
        var catchUp = rewards.CrewPaidSoFar + rewards.CatchUpPerWave * Math.Max(0, wavesMissed);
        if (catchUp > 0)
            _wallet.GiveCredits(mindId, catchUp);
    }

    private void PayCrew(FSWaveRewardsComponent rewards, int amount)
    {
        _wallet.DistributeCredits(amount);
        rewards.CrewPaidSoFar += amount;
    }

    private bool IsSecurity(EntityUid mindId)
    {
        return _jobs.MindTryGetJob(mindId, out var job)
               && _jobs.TryGetPrimaryDepartment(job.ID, out var department)
               && department.ID == SecurityDepartment;
    }

    // What killing this enemy pays now, before the killer's own bonuses; 0 outside a wave rule.
    public int KillCredits(EntityUid enemy)
    {
        return TryGetRewards(out var rewards) ? KillCredits(enemy, rewards) : 0;
    }

    private int KillCredits(EntityUid enemy, FSWaveRewardsComponent rewards)
    {
        var value = TryComp<FSEnemyValueComponent>(enemy, out var enemyValue)
            ? enemyValue.KillCredits
            : rewards.KillReward;
        return (int) (value * KillMultiplier(_waveRule.GetWaveNumber()) * rewards.KillShare
            * _waveRule.GetRewardMultiplier() * _waveRule.GetEnemyMoneyMultiplier());
    }

    public static float KillMultiplier(int wave)
    {
        foreach (var (upToWave, multiplier) in KillMultipliers)
        {
            if (wave <= upToWave)
                return multiplier;
        }

        return LateKillMultiplier;
    }
}
