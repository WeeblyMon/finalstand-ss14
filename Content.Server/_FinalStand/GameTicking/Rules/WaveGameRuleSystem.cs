using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.RoundEnd;
using Content.Server._FinalStand.Cleanup;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.FriendlyFire;
using Content.Server._FinalStand.Spawners;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.WaveHud;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed partial class WaveGameRuleSystem : GameRuleSystem<WaveGameRuleComponent>
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSCorpseCleanupSystem _corpseCleaner = default!;
    [Dependency] private FSFriendlyFireSystem _friendlyFire = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private WaveEnemySpawningSystem _spawning = default!;
    [Dependency] private FSDarkWaveSystem _darkWave = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    private static readonly TimeSpan DarkWaveSpawnDelay = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan NewPlayerScalingExemption = TimeSpan.FromHours(10);

    private static readonly TimeSpan VoteCountdown = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan WipeRoundEndCountdown = TimeSpan.FromSeconds(30);


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, MobStateChangedEvent>(OnWaveEnemyMobStateChanged);
        SubscribeLocalEvent<WaveSpawnedTagComponent, ComponentShutdown>(OnWaveEnemyShutdown);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    // Raised before the entity flush. The rule is still active while every enemy is deleted, and the last one
    // going would otherwise end the wave and start prep in the middle of the restart.
    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        var query = QueryAllRules();
        while (query.MoveNext(out _, out var comp, out _))
            comp.AliveEnemies.Clear();
    }

    protected override void Started(EntityUid uid, WaveGameRuleComponent comp,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _friendlyFire.AssignFactionToAllPlayers();
        EnsureComp<FSWaveStateComponent>(uid);
        _pvsOverride.AddGlobalOverride(uid);
        StartPrepPhase(uid, comp);
        SyncState(uid, comp);
    }

    protected override void ActiveTick(EntityUid uid, WaveGameRuleComponent comp,
        GameRuleComponent gameRule, float frameTime)
    {
        var now = Timing.CurTime;

        switch (comp.Phase)
        {
            case WavePhase.Prep:
                if (comp.SpawnerEntities.Count == 0 && now >= comp.NextSpawnerRetryTime)
                {
                    comp.NextSpawnerRetryTime = now + TimeSpan.FromSeconds(1);
                    _spawning.SelectSpawners(comp, CountScalingPlayers());
                }
                if (comp.VoteCountdownActive && !comp.VoteCountdownSoundPlayed && now >= comp.VoteCountdownSoundTime)
                {
                    comp.VoteCountdownSoundPlayed = true;
                    if (comp.WaveVoteCountdownSound != null)
                        _audio.PlayGlobal(comp.WaveVoteCountdownSound, Filter.Broadcast(), true);
                }
                if (now >= comp.PhaseEndTime)
                {
                    if (!comp.VoteCountdownActive)
                    {
                        Log.Info($"Prep timer at zero for wave {comp.WaveNumber}, starting 10s countdown.");
                        comp.VoteCountdownActive = true;
                        comp.VoteCountdownSoundPlayed = false;
                        comp.PhaseEndTime = now + TimeSpan.FromSeconds(10);
                        comp.VoteCountdownSoundTime = now + TimeSpan.FromSeconds(2);
                    }
                    else
                    {
                        Log.Info($"Countdown ended for wave {comp.WaveNumber}, starting combat.");
                        StartCombatPhase(uid, comp);
                    }
                    break;
                }
                break;

            case WavePhase.Combat:
                if (now >= comp.PhaseEndTime)
                {
                    Log.Warning($"Wave {comp.WaveNumber} fallback timer expired. Forcing end.");
                    // ClearCombat deletes whatever is still alive (stuck enemies included).
                    EndCombatPhase(uid, comp);
                    break;
                }

                if (!comp.SpawnPaused && comp.EnemiesSpawnedThisWave < comp.EnemyTotalThisWave && now >= comp.NextSpawnTime)
                {
                    _spawning.SpawnNextBatch(uid, comp);
                    CheckWaveComplete(uid, comp);
                }

                if (now >= comp.NextHeartbeatTime)
                {
                    if (_darkWave.IsActive(uid) && !comp.RoundLost && IsTeamWiped())
                    {
                        comp.RoundLost = true;
                        Log.Info($"Every player died during dark wave {comp.WaveNumber}. Ending round.");
                        _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-wave-team-wiped"), Color.Red);
                        _roundEnd.EndRound(WipeRoundEndCountdown);
                    }

                    var timeLeft = comp.PhaseEndTime - now;
                    Log.Debug($"Wave {comp.WaveNumber} | " +
                             $"spawned {comp.EnemiesSpawnedThisWave}/{comp.EnemyTotalThisWave} | " +
                             $"alive {comp.AliveEnemies.Count} | " +
                             $"fallback in {timeLeft.TotalSeconds:F0}s");
                    comp.NextHeartbeatTime = now + TimeSpan.FromSeconds(5);
                }
                break;
        }

        SyncState(uid, comp);
    }

    // Copies what clients display onto the networked state, dirtying only fields that changed.
    private void SyncState(EntityUid uid, WaveGameRuleComponent comp)
    {
        if (!TryComp<FSWaveStateComponent>(uid, out var state))
            return;

        var dark = _darkWave.IsActive(uid);
        var wave = comp.Phase == WavePhase.Prep ? comp.WavesCompleted : comp.WaveNumber;
        var endsAt = comp.PhaseEndTime;
        var alive = comp.Phase == WavePhase.Combat ? comp.AliveEnemies.Count : 0;
        var total = comp.Phase == WavePhase.Combat && !dark ? comp.EnemyTotalThisWave : 0;

        if (state.Wave != wave)
        {
            state.Wave = wave;
            DirtyField(uid, state, nameof(FSWaveStateComponent.Wave));
        }

        if (state.Phase != comp.Phase)
        {
            state.Phase = comp.Phase;
            DirtyField(uid, state, nameof(FSWaveStateComponent.Phase));
        }

        if (state.PhaseEndsAt != endsAt)
        {
            state.PhaseEndsAt = endsAt;
            DirtyField(uid, state, nameof(FSWaveStateComponent.PhaseEndsAt));
        }

        if (state.EnemiesAlive != alive)
        {
            state.EnemiesAlive = alive;
            DirtyField(uid, state, nameof(FSWaveStateComponent.EnemiesAlive));
        }

        if (state.EnemiesTotal != total)
        {
            state.EnemiesTotal = total;
            DirtyField(uid, state, nameof(FSWaveStateComponent.EnemiesTotal));
        }

        if (state.DarkWave != dark)
        {
            state.DarkWave = dark;
            DirtyField(uid, state, nameof(FSWaveStateComponent.DarkWave));
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, WaveGameRuleComponent comp,
        GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, comp, gameRule, ref args);
        _wallet.SaveAll();
        args.AddLine(Loc.GetString("final-stand-round-end",
            ("wave", comp.WavesCompleted),
            ("killed", comp.TotalEnemiesKilled)));
    }

    private bool TryGetActiveRule(out EntityUid uid, out WaveGameRuleComponent comp)
    {
        var query = QueryActiveRules();
        if (query.MoveNext(out uid, out _, out comp!, out _))
            return true;

        uid = EntityUid.Invalid;
        return false;
    }

    private void StartPrepPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Prep;
        comp.VoteCountdownActive = false;
        comp.VoteCountdownSoundPlayed = false;
        comp.VoteCountdownSoundTime = TimeSpan.Zero;
        comp.PhaseEndTime = Timing.CurTime + comp.PrepDuration;
        comp.PrepEndUnshaved = comp.PhaseEndTime;
        _darkWave.Roll(uid, comp.WaveNumber, IsBossWave(comp.WaveNumber));

        _spawning.SelectSpawners(comp, CountScalingPlayers());
        if (comp.SpawnerEntities.Count == 0)
            Log.Warning($"No WaveEnemySpawner entities found! Wave {comp.WaveNumber} will be empty.");

        Log.Info($"Prep phase started. Wave {comp.WaveNumber} begins in {comp.PrepDuration.TotalSeconds}s. " +
                 $"Pre-selected {comp.SpawnerEntities.Count} spawner(s).");

        comp.EnemyTotalThisWave = CalcEnemyTotal(comp, CountScalingPlayers(), CountPrimarySpawners(comp));
        comp.EnemiesSpawnedThisWave = 0;

        if (comp.WavesCompleted > 0 && comp.WaveEndSound != null)
            _audio.PlayGlobal(comp.WaveEndSound, Filter.Broadcast(), true);
        var prepStarted = new WavePrepStartedEvent();
        RaiseLocalEvent(ref prepStarted);
    }

    private static int CalcEnemyTotal(WaveGameRuleComponent comp, int players, int primarySpawners)
    {
        var playerBonus = comp.WaveNumber >= comp.PlayerBonusFromWave
            ? players * comp.PlayerEnemyBonus
            : 0;
        var total = Math.Min((int)((4.4f * comp.WaveNumber + 7 + playerBonus) * 1.5f), comp.MaxEnemyCap);
        if (comp.WaveNumber >= 5 && primarySpawners == 1)
            total = Math.Min(total * 2, comp.MaxEnemyCap);
        return total;
    }

    private int CountPrimarySpawners(WaveGameRuleComponent comp)
    {
        var count = 0;
        foreach (var spawnerUid in comp.SpawnerEntities)
        {
            if (!TryComp<WaveEnemySpawnerComponent>(spawnerUid, out var spawner) || !spawner.Secondary)
                count++;
        }
        return count;
    }

    private void StartCombatPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Combat;
        comp.CombatStartTime = Timing.CurTime;

        if (comp.SpawnerEntities.Count == 0)
            _spawning.SelectSpawners(comp, CountScalingPlayers());

        comp.CCCEntity = EntityUid.Invalid;
        var cq = EntityQueryEnumerator<FinalStandCCCComponent>();
        if (cq.MoveNext(out var cccUid, out _))
            comp.CCCEntity = cccUid;

        if (!comp.CCCEntity.IsValid())
            Log.Warning("No FinalStandCCC entity found — enemies will not beeline to objective.");

        // Fixed for the wave. Sessions alone would count the lobby, ghosts and admins.
        comp.ScalingPlayersThisWave = CountScalingPlayers();

        comp.EnemyTotalThisWave = CalcEnemyTotal(comp, comp.ScalingPlayersThisWave, CountPrimarySpawners(comp));
        comp.EnemiesSpawnedThisWave = 0;
        comp.AliveEnemies.Clear();

        comp.GiantEntity = EntityUid.Invalid;
        comp.GiantApAwarded = false;
        if (IsBossWave(comp.WaveNumber) && comp.SpawnerEntities.Count > 0 && comp.BossPool.Count > 0)
        {
            var spawnerUid = comp.SpawnerEntities[0];
            var bossProto = RobustRandom.Pick(comp.BossPool);
            var giant = _spawning.SpawnWaveEnemy(bossProto, Transform(spawnerUid).Coordinates, comp);
            comp.AliveEnemies.Add(giant);
            comp.GiantEntity = giant;
            comp.EnemyTotalThisWave++;
            comp.EnemiesSpawnedThisWave++;
            Log.Info($"Boss wave {comp.WaveNumber}: spawned {bossProto} ({giant}) at spawner {spawnerUid}.");
        }
        comp.PhaseEndTime = Timing.CurTime + comp.MaxCombatDuration;
        comp.NextSpawnTime = Timing.CurTime;

        // Dark waves have no enemy total; they last a fixed time instead.
        var dark = _darkWave.TryBegin(uid, out var darkDuration);
        if (dark)
        {
            comp.EnemyTotalThisWave = int.MaxValue;
            comp.NextSpawnTime = Timing.CurTime + DarkWaveSpawnDelay;
            comp.PhaseEndTime = Timing.CurTime + darkDuration;
            Log.Info($"Dark wave started for wave {comp.WaveNumber}, lasting {darkDuration.TotalSeconds}s.");
        }

        var pool = WaveEnemySpawningSystem.GetDirectorPool(comp);
        Log.Info($"Wave {comp.WaveNumber} started. Spawning {comp.EnemyTotalThisWave} enemies " +
                 $"at {comp.SpawnerEntities.Count} spawners. Director pool: {pool.Count} type(s).");

        if (!dark && comp.WaveStartSound != null)
            _audio.PlayGlobal(comp.WaveStartSound, Filter.Broadcast(), true);
        var combatStarted = new WaveCombatStartedEvent();
        RaiseLocalEvent(ref combatStarted);
    }

    private void EndCombatPhase(EntityUid uid, WaveGameRuleComponent comp, bool isForced = false)
    {
        Log.Info($"Wave {comp.WaveNumber} complete. Moving to prep for wave {comp.WaveNumber + 1}.");
        ClearCombat(uid, comp);

        if (!isForced)
        {
            var ended = new WaveEndedEvent(comp.WaveNumber);
            RaiseLocalEvent(ref ended);
        }
        if (IsBossWave(comp.WaveNumber) && !comp.GiantApAwarded)
        {
            Log.Warning($"Wave {comp.WaveNumber} boss never died — paying the reward on wave end.");
            AwardBossReward(comp);
        }
        comp.GiantEntity = EntityUid.Invalid;
        comp.GiantApAwarded = false;
        comp.WavesCompleted++;
        comp.WaveNumber++;
        StartPrepPhase(uid, comp);
    }

    public int? FailWave()
    {
        if (!TryGetActiveRule(out var uid, out var comp) || comp.Phase != WavePhase.Combat)
            return null;

        Log.Info($"Wave {comp.WaveNumber} lost with the CCC. Back to prep for the same wave.");
        ClearCombat(uid, comp);
        comp.GiantEntity = EntityUid.Invalid;
        comp.GiantApAwarded = false;
        StartPrepPhase(uid, comp);
        return comp.WaveNumber;
    }

    private void ClearCombat(EntityUid uid, WaveGameRuleComponent comp)
    {
        foreach (var enemy in comp.AliveEnemies)
            QueueDel(enemy);
        comp.AliveEnemies.Clear();

        _darkWave.End(uid);
    }

    private void AwardBossReward(WaveGameRuleComponent comp)
    {
        comp.GiantApAwarded = true;
        if (comp.BossWavePerkReward <= 0)
            return;

        _wallet.DistributePerkPoints(comp.BossWavePerkReward);
        Log.Info($"Boss wave {comp.WaveNumber}: awarded {comp.BossWavePerkReward} PP.");
    }

    private int CountScalingPlayers()
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;
            if (!HasComp<MobStateComponent>(mob) || _mobState.IsDead(mob))
                continue;
            if (IsNewPlayer(session))
                continue;
            count++;
        }
        return count;
    }

    private bool IsTeamWiped()
    {
        var anyPlayer = false;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } attached)
                continue;

            anyPlayer = true;
            if (HasComp<MobStateComponent>(attached) && !_mobState.IsDead(attached))
                return false;
        }

        return anyPlayer;
    }

    private bool IsNewPlayer(ICommonSession session)
    {
        if (!_playTime.TryGetTrackerTimes(session, out var times))
            return false;

        return times.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall) < NewPlayerScalingExemption;
    }

    private static bool IsBossWave(int wave) => wave % 5 == 0;



    private void CheckWaveComplete(EntityUid uid, WaveGameRuleComponent comp)
    {
        if (_darkWave.IsActive(uid))
            return;
        if (comp.EnemiesSpawnedThisWave >= comp.EnemyTotalThisWave && comp.AliveEnemies.Count == 0)
            EndCombatPhase(uid, comp);
    }

    private void OnWaveEnemyMobStateChanged(Entity<WaveSpawnedTagComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        _corpseCleaner.TrackZombieDeath(ent.Owner);

        var died = new FSWaveEnemyDiedEvent(ent.Owner, args.Origin);
        RaiseLocalEvent(ref died);

        if (TryComp<FixturesComponent>(ent.Owner, out var fixtures))
        {
            foreach (var (key, fixture) in fixtures.Fixtures)
            {
                _physics.SetCollisionLayer(ent.Owner, key, fixture, 0, fixtures);
                _physics.SetCollisionMask(ent.Owner, key, fixture, 0, fixtures);
            }
        }

        if (!TryGetActiveRule(out var uid, out var comp) || !comp.AliveEnemies.Remove(ent.Owner))
            return;

        comp.TotalEnemiesKilled++;

        if (ent.Owner == comp.GiantEntity && !comp.GiantApAwarded)
            AwardBossReward(comp);

        CheckWaveComplete(uid, comp);
    }

    private void OnWaveEnemyShutdown(EntityUid uid, WaveSpawnedTagComponent comp, ComponentShutdown args)
    {
        if (!TryGetActiveRule(out var ruleUid, out var ruleComp) || !ruleComp.AliveEnemies.Remove(uid))
            return;

        CheckWaveComplete(ruleUid, ruleComp);
    }

    // The Captain hit Start, or most of the crew readied up.
    public void RequestWaveStart()
    {
        if (!TryGetActiveRule(out _, out var comp) || comp.Phase != WavePhase.Prep || comp.VoteCountdownActive)
            return;

        // The countdown itself doesn't count as time saved.
        var skipped = comp.PrepEndUnshaved - Timing.CurTime - VoteCountdown;
        if (skipped > TimeSpan.Zero)
        {
            var skippedEv = new WavePrepSkippedEvent(skipped);
            RaiseLocalEvent(ref skippedEv);
        }

        comp.VoteCountdownActive = true;
        comp.VoteCountdownSoundPlayed = false;
        comp.PhaseEndTime = Timing.CurTime + VoteCountdown;
        comp.VoteCountdownSoundTime = Timing.CurTime + TimeSpan.FromSeconds(2);
    }

    public record struct CCCStateData(
        int WaveNumber,
        int TotalEnemies,
        string SpawnerDirections,
        bool IsBossWave,
        LocId FactionDisplay,
        List<string> NextWaveEnemyTypes);

    public bool TryGetActiveState(out CCCStateData data)
    {
        data = default;
        if (!TryGetActiveRule(out var uid, out var comp))
            return false;

        var dark = _darkWave.IsActive(uid);

        var pool = WaveEnemySpawningSystem.GetDirectorPool(comp);
        var types = pool.Select(p => p.Id).Distinct().ToList();
        if (IsBossWave(comp.WaveNumber))
            types.InsertRange(0, comp.BossPool.Select(p => p.Id).Distinct());
        foreach (var special in comp.SpecialEnemyPool)
        {
            if (comp.WaveNumber >= special.FromWave)
                types.Add(special.EnemyId.Id);
        }
        var directions = dark ? Loc.GetString("fs-ccc-spawners-all") : _spawning.GetSpawnDirections(comp);
        data = new CCCStateData(
            comp.WaveNumber,
            comp.EnemyTotalThisWave,
            directions,
            IsBossWave(comp.WaveNumber),
            comp.FactionDisplay,
            types);
        return true;
    }

    // Returns the wave the rule is on afterwards, or null if no wave rule is running.
    public int? ForceNextWave(int? targetWave = null)
    {
        if (!TryGetActiveRule(out var uid, out var comp))
            return null;

        if (comp.Phase == WavePhase.Combat)
        {
            // Ending combat moves on a wave, so aim one short to land in prep for the target.
            if (targetWave is { } next)
            {
                comp.WaveNumber = next - 1;
                comp.WavesCompleted = next - 2;
            }

            foreach (var enemy in comp.AliveEnemies)
                QueueDel(enemy);
            comp.AliveEnemies.Clear();
            comp.GiantApAwarded = true;
            EndCombatPhase(uid, comp, isForced: true);
        }
        else
        {
            if (targetWave is { } wave && wave != comp.WaveNumber)
            {
                comp.WaveNumber = wave;
                comp.WavesCompleted = wave - 1;
                // The dark wave and the lanes were picked for the wave we skipped.
                _darkWave.Roll(uid, wave, IsBossWave(wave));
                _spawning.SelectSpawners(comp, CountScalingPlayers());
            }

            StartCombatPhase(uid, comp);
        }

        return comp.WaveNumber;
    }

    public bool IsPrep()
    {
        return TryGetActiveRule(out _, out var comp) && comp.Phase == WavePhase.Prep;
    }

    public int GetWaveNumber()
    {
        return TryGetActiveRule(out _, out var comp) ? comp.WaveNumber : 0;
    }

    public bool TryGetWaveStatus(out int wave, out WavePhase phase)
    {
        if (!TryGetActiveRule(out _, out var comp))
        {
            wave = 0;
            phase = WavePhase.Prep;
            return false;
        }

        wave = comp.WaveNumber;
        phase = comp.Phase;
        return true;
    }

    // Each crew member readying up takes their share of the prep time off the clock.
    public void ShavePrepTime(int crewCount)
    {
        if (crewCount <= 0
            || !TryGetActiveRule(out _, out var comp)
            || comp.Phase != WavePhase.Prep
            || comp.VoteCountdownActive)
        {
            return;
        }

        comp.PhaseEndTime -= comp.PrepDuration / crewCount;
        if (comp.PhaseEndTime < Timing.CurTime)
            comp.PhaseEndTime = Timing.CurTime;
    }

    public bool TryToggleSpawnPause(out bool paused)
    {
        paused = false;
        if (!TryGetActiveRule(out _, out var comp))
            return false;

        comp.SpawnPaused = !comp.SpawnPaused;
        paused = comp.SpawnPaused;
        return true;
    }
}
