using Content.Server._FinalStand.GameTicking.Rules;
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
        SubscribeLocalEvent<FSEnemyDamageTrackingComponent, BeforeDamageChangedEvent>(OnEnemyBeforeDamage);
        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnEnemyDied);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<WavePrepSkippedEvent>(OnPrepSkipped);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private bool TryGetRewards(out FSWaveRewardsComponent rewards)
    {
        var query = EntityQueryEnumerator<FSWaveRewardsComponent, ActiveGameRuleComponent>();
        if (query.MoveNext(out rewards!, out _))
            return true;

        return false;
    }

    private void OnEnemyBeforeDamage(Entity<FSEnemyDamageTrackingComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!args.Cancelled && args.Origin is { } origin && _mind.TryGetMind(origin, out var mindId, out _))
            ent.Comp.AttackerMinds.Add(mindId);
    }

    private void OnEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (!TryGetRewards(out var rewards))
            return;

        var value = TryComp<FSEnemyValueComponent>(args.Enemy, out var enemyValue)
            ? enemyValue.KillCredits
            : rewards.KillReward;
        var killCredits = (int) (value * KillMultiplier(_waveRule.GetWaveNumber()) * rewards.KillShare);

        EntityUid? killerMind = null;
        if (args.Killer is { } killer && _mind.TryGetMind(killer, out var mindId, out _))
        {
            killerMind = mindId;
            _wallet.GiveCredits(mindId, killCredits);

            if (IsSecurity(mindId))
                _wallet.GiveCredits(mindId, rewards.SecurityKillBonus);
        }

        if (!TryComp<FSEnemyDamageTrackingComponent>(args.Enemy, out var tracking))
            return;

        var assistCredits = (int) (killCredits * rewards.AssistShare);
        foreach (var assistMind in tracking.AttackerMinds)
        {
            if (assistMind != killerMind)
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
            || !rewards.CaughtUpMinds.Add(mindId))
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

    private static float KillMultiplier(int wave)
    {
        foreach (var (upToWave, multiplier) in KillMultipliers)
        {
            if (wave <= upToWave)
                return multiplier;
        }

        return LateKillMultiplier;
    }
}
