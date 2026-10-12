using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Onboarding;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Tutorial;

public sealed partial class FSTutorialSystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSOnboardingSystem _onboarding = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public const string OptOutFlag = "_off";
    public const string OptInFlag = "_on";

    private readonly Dictionary<NetUserId, HashSet<string>> _cache = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawned,
            after: new[] { typeof(FSOnboardingSystem) });
        SubscribeLocalEvent<WavePrepStartedEvent>(_ => ShowToAll(FSTutorialTip.Prep));
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _cache.Clear());
    }

    private HashSet<string> Flags(NetUserId user)
    {
        if (!_cache.TryGetValue(user, out var flags))
            _cache[user] = flags = _store.GetTutorialFlags(user.UserId);
        return flags;
    }

    public bool WantsTips(ICommonSession session)
    {
        var flags = Flags(session.UserId);
        if (flags.Contains(OptOutFlag))
            return false;
        return flags.Contains(OptInFlag) || _onboarding.IsRookie(session);
    }

    public bool HasSeen(NetUserId user, FSTutorialTip tip) => Flags(user).Contains(FSTutorialText.Key(tip));

    public bool TryShow(ICommonSession session, FSTutorialTip tip)
    {
        if (session.Status != SessionStatus.InGame || !WantsTips(session))
            return false;

        var key = FSTutorialText.Key(tip);
        if (!Flags(session.UserId).Add(key))
            return false;

        _store.SetTutorialFlag(session.UserId.UserId, key, true);
        RaiseNetworkEvent(new FSTutorialTipEvent { Tip = tip }, session);
        return true;
    }

    public void SetEnabled(NetUserId user, bool enabled)
    {
        var flags = Flags(user);
        var add = enabled ? OptInFlag : OptOutFlag;
        var remove = enabled ? OptOutFlag : OptInFlag;
        flags.Add(add);
        flags.Remove(remove);
        _store.SetTutorialFlag(user.UserId, add, true);
        _store.SetTutorialFlag(user.UserId, remove, false);
    }

    public void Replay(NetUserId user)
    {
        _store.ClearTutorial(user.UserId);
        _cache[user] = new HashSet<string> { OptInFlag };
        _store.SetTutorialFlag(user.UserId, OptInFlag, true);
    }

    private void ShowToAll(FSTutorialTip tip)
    {
        foreach (var session in _playerManager.Sessions.Where(s => s.AttachedEntity != null))
            TryShow(session, tip);
    }

    private void OnSpawned(PlayerSpawnCompleteEvent ev)
    {
        TryShow(ev.Player, FSTutorialTip.Welcome);
        if (_waveRule.TryGetWaveStatus(out _, out var phase) && phase == WavePhase.Prep)
            TryShow(ev.Player, FSTutorialTip.Prep);
    }

    private void OnCombatStarted(WaveCombatStartedEvent ev)
    {
        if (!_waveRule.TryGetActiveState(out var state))
            return;

        ShowToAll(state.IsDarkWave ? FSTutorialTip.DarkWave
            : state.IsBossWave ? FSTutorialTip.BossWave
            : FSTutorialTip.FirstWave);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev) => ShowToAll(FSTutorialTip.Perks);

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Critical || !TryComp<ActorComponent>(ev.Target, out var actor))
            return;

        TryShow(actor.PlayerSession, FSTutorialTip.Downed);
    }
}
