using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Objectives;

public sealed partial class FSWaveObjectiveSystem : EntitySystem
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSTitleBuffSystem _titleBuffs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color AnnounceColor = Color.FromHex("#4FC3F7");

    public sealed class ActiveObjective
    {
        public required FSWaveObjectivePrototype Prototype;
        public required int Wave;
        public required int Target;
        public int Progress;
        public TimeSpan? EndTime { get; set; }
        public FSWaveObjectiveResult Result = FSWaveObjectiveResult.InProgress;
        public int Players;
        public readonly HashSet<EntityUid> Marked = new();
        public readonly HashSet<NetUserId> Hurt = new();
        public EntityUid? Ccc;
        public float CccStartDamage;
        public EntityUid? Supply;
    }

    public ActiveObjective? Current { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<WavePrepStartedEvent>(_ => Clear());
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnEnemyDied);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeNetworkEvent<FSWaveHudRequestEvent>((_, args) => SendState(Filter.SinglePlayer(args.SenderSession)));
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Clear());
        InitializeKinds();
    }

    private void OnCombatStarted(WaveCombatStartedEvent ev)
    {
        Clear();
        var chance = _cfg.GetCVar(CCVars.FSWaveObjectiveChance);
        if (chance <= 0f || !_random.Prob(Math.Min(1f, chance)))
            return;

        TryStartRandom();
    }

    public bool TryStartRandom()
    {
        if (!_waveRule.TryGetActiveState(out var state) || state.Phase != WavePhase.Combat)
            return false;

        var pool = _proto.EnumeratePrototypes<FSWaveObjectivePrototype>()
            .Where(o => FSWaveObjectiveRules.Eligible(o.MinWave, state.WaveNumber, state.IsBossWave, state.IsDarkWave))
            .OrderBy(o => o.ID)
            .ToList();

        var picked = FSWaveObjectiveRules.Pick(pool, o => o.Weight, _random.NextFloat());
        return picked != null && TryStart(picked);
    }

    public bool TryStart(FSWaveObjectivePrototype objective)
    {
        if (!_waveRule.TryGetActiveState(out var state) || state.Phase != WavePhase.Combat)
            return false;

        Clear();
        var players = CountPlayers();
        Current = new ActiveObjective
        {
            Prototype = objective,
            Wave = state.WaveNumber,
            Players = players,
            Target = FSWaveObjectiveRules.Target(objective.Count, objective.CountPerExtraPlayer, players),
            EndTime = objective.Seconds > 0f ? _timing.CurTime + TimeSpan.FromSeconds(objective.Seconds) : null,
        };
        SetUp(Current);

        _chat.DispatchServerAnnouncement(Loc.GetString("fs-objective-announce",
            ("name", Loc.GetString(objective.Name)),
            ("description", Loc.GetString(objective.Description,
                ("count", Current.Target), ("seconds", (int) objective.Seconds))),
            ("credits", RewardFor(Current))), AnnounceColor);
        SendState(Filter.Broadcast());
        return true;
    }

    private int RewardFor(ActiveObjective objective)
        => FSWaveObjectiveRules.Reward(objective.Prototype.Credits, objective.Prototype.CreditsPerWave, objective.Wave,
            _waveRule.GetRewardMultiplier());

    private int CountPlayers()
        => _playerManager.Sessions.Count(s => s.Status == SessionStatus.InGame
            && s.AttachedEntity is { } body && HasComp<MobStateComponent>(body));

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (Current is not { Result: FSWaveObjectiveResult.InProgress } current)
            return;

        UpdateKinds(current);
        if (current.Result == FSWaveObjectiveResult.InProgress && current.EndTime is { } end && _timing.CurTime >= end)
            Fail();
    }

    private void OnEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress } current)
            return;

        if (current.Prototype.Kind != FSWaveObjectiveKind.KillQuota)
        {
            OnEnemyDiedKinds(current, args);
            return;
        }

        if (args.Killer is not { } killer
            || !_mind.TryGetMind(killer, out _, out var mind) || mind.UserId == null)
            return;

        current.Progress++;
        if (current.Progress >= current.Target)
            Complete();
        else
            SendState(Filter.Broadcast());
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress } current
            || current.Prototype.Kind != FSWaveObjectiveKind.NoDowns
            || args.NewMobState == MobState.Alive
            || !HasComp<ActorComponent>(args.Target))
            return;

        Fail();
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress } current)
            return;

        if (FSWaveObjectiveRules.CompletesAtWaveEnd(current.Prototype.Kind))
            Complete();
        else
            Fail();
    }

    private void Complete()
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress } current)
            return;

        current.Result = FSWaveObjectiveResult.Completed;
        TearDown(current, completed: true);
        var reward = RewardFor(current);
        _wallet.DistributeCredits(reward, mindId => _titleBuffs.ForMind(mindId).ObjectiveCredits);
        _chat.DispatchServerAnnouncement(Loc.GetString("fs-objective-complete",
            ("name", Loc.GetString(current.Prototype.Name)), ("credits", reward)), AnnounceColor);
        SendState(Filter.Broadcast());
    }

    private void Fail()
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress } current)
            return;

        current.Result = FSWaveObjectiveResult.Failed;
        TearDown(current, completed: false);
        _chat.DispatchServerAnnouncement(Loc.GetString("fs-objective-failed",
            ("name", Loc.GetString(current.Prototype.Name))), Color.Gray);
        SendState(Filter.Broadcast());
    }

    private void Clear()
    {
        if (Current == null)
            return;

        TearDown(Current, completed: Current.Result == FSWaveObjectiveResult.Completed);
        Current = null;
        SendState(Filter.Broadcast());
    }

    private void SendState(Filter filter)
    {
        var ev = Current is not { } current
            ? new FSWaveObjectiveStateEvent()
            : new FSWaveObjectiveStateEvent
            {
                Active = true,
                Name = Loc.GetString(current.Prototype.Name),
                Kind = current.Prototype.Kind,
                Progress = current.Progress,
                Target = current.Target,
                EndTime = current.EndTime,
                Result = current.Result,
                Reward = RewardFor(current),
            };
        RaiseNetworkEvent(ev, filter);
    }
}
