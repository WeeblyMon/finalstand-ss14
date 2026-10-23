using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Onboarding;

[ByRefEvent]
public readonly record struct FSRookieFirstRoundEvent(NetUserId Rookie);

public sealed partial class FSOnboardingSystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private FSPlayerBadgeSystem _badge = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private FSRoundPresenceSystem _presence = default!;
    [Dependency] private FSLevelingSystem _leveling = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete,
            after: new[] { typeof(FSPlayerWalletSystem), typeof(FSLevelingSystem), typeof(FSTitleSystem) });
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    public bool TryGetRookie(ICommonSession session, out bool rookie)
    {
        rookie = false;
        if (!_playTime.TryGetTrackerTimes(session, out var times))
            return false;

        rookie = FSRookieRules.IsRookie(times.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall));
        return true;
    }

    public bool IsRookie(ICommonSession session)
        => TryGetRookie(session, out var rookie) && rookie;

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        RefreshPlayer(ev.Player, mindId, ev.Mob);
    }

    public void RefreshPlayer(ICommonSession session, EntityUid mindId, EntityUid? body)
    {
        if (!TryGetRookie(session, out var rookie))
            return;

        var userId = session.UserId;
        var record = _store.GetOnboarding(userId.UserId);
        if (!_store.IsProgressReadable(userId.UserId))
            return;

        if (!record.Known)
        {
            record = rookie
                ? new FSPlayerDataStore.OnboardingRecord(true, true, false, false, false)
                : new FSPlayerDataStore.OnboardingRecord(true, false, true, true, true);
        }

        if (rookie && !record.StarterGranted)
        {
            _wallet.AddPerkPoints(mindId, FSRookieRules.StarterPerkPoints);
            record = record with { StarterGranted = true };
            _chat.DispatchServerMessage(session, Loc.GetString("fs-onboarding-starter-grant",
                ("points", FSRookieRules.StarterPerkPoints),
                ("hours", (int) FSRookieRules.RookieThreshold.TotalHours)));
        }

        if (!rookie && record.WasRookie && !record.Graduated)
            record = Graduate(session, mindId, record);

        _store.SaveOnboarding(userId.UserId, record);
        ApplyRookieState(mindId, body, rookie);
    }

    private FSPlayerDataStore.OnboardingRecord Graduate(ICommonSession session, EntityUid mindId,
        FSPlayerDataStore.OnboardingRecord record)
    {
        _wallet.AddPerkPoints(mindId, FSRookieRules.GraduationPerkPoints);
        _titles.Grant(session.UserId, FSRookieRules.GraduationTitle);
        _chat.DispatchServerMessage(session, Loc.GetString("fs-onboarding-graduated",
            ("points", FSRookieRules.GraduationPerkPoints)));
        Log.Info($"[FSOnboarding] {session.Name} graduated from rookie status.");
        return record with { Graduated = true };
    }

    private void ApplyRookieState(EntityUid mindId, EntityUid? body, bool rookie)
    {
        if (TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
        {
            lvl.CatchUpMultiplier = rookie ? FSRookieRules.CatchUpXpMultiplier : 1f;
            _leveling.SendLevelingUpdate(mindId, lvl);
        }

        if (body is not { } mob || TerminatingOrDeleted(mob))
            return;

        var badge = EnsureComp<FSPlayerBadgeComponent>(mob);
        _badge.SetBadge(mob, rookie, badge.Title);
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity == null)
                continue;
            if (!_mind.TryGetMind(session, out var mindId, out _) || !HasComp<FSPlayerLevelComponent>(mindId))
                continue;
            if (!TryGetRookie(session, out var rookie) || rookie)
                continue;

            var record = _store.GetOnboarding(session.UserId.UserId);
            if (!_store.IsProgressReadable(session.UserId.UserId) || !record.WasRookie || record.Graduated)
                continue;

            _store.SaveOnboarding(session.UserId.UserId, Graduate(session, mindId, record));
            ApplyRookieState(mindId, session.AttachedEntity, false);
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (!_mind.TryGetMind(session, out var mindId, out _))
                continue;

            if (TryPayFirstRound(session, mindId))
                args.AddLine(Loc.GetString("fs-onboarding-first-round-line", ("name", session.Name),
                    ("points", FSRookieRules.FirstRoundPerkPoints)));
        }
    }

    public bool TryPayFirstRound(ICommonSession session, EntityUid mindId)
    {
        var record = _store.GetOnboarding(session.UserId.UserId);
        if (!record.Known || !record.WasRookie || record.FirstRoundPaid)
            return false;
        if (_presence.GetWavesPresent(session.UserId) < FSRookieRules.FirstRoundMinWaves)
            return false;

        _wallet.AddPerkPoints(mindId, FSRookieRules.FirstRoundPerkPoints);
        _store.SaveOnboarding(session.UserId.UserId, record with { FirstRoundPaid = true });
        _titles.Grant(session.UserId, FSRookieRules.FirstRoundTitle);
        _chat.DispatchServerMessage(session, Loc.GetString("fs-onboarding-first-round",
            ("points", FSRookieRules.FirstRoundPerkPoints)));
        var firstRound = new FSRookieFirstRoundEvent(session.UserId);
        RaiseLocalEvent(ref firstRound);
        return true;
    }
}
