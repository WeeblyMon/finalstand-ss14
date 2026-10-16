using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Onboarding;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Damage.Systems;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mentor;

public sealed partial class FSMentorSystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSOnboardingSystem _onboarding = default!;
    [Dependency] private FSRoundPresenceSystem _presence = default!;
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;

    public const int ReviveMp = 3;
    public const int ReviveCredits = 300;
    public const float HealPerMp = 50f;
    public const int HealCreditsPerMp = 50;
    public const int WaveEscortMp = 1;
    public const float WaveEscortRange = 8f;
    public const int FirstRoundMp = 5;
    public static readonly TimeSpan ReviveCooldown = TimeSpan.FromSeconds(120);

    public readonly record struct ShopItem(string Id, int Cost, string Title);

    public static readonly ShopItem[] Shop =
    {
        new("mentor", 50, "FSTitleMentor"),
        new("guardian", 400, "FSTitleGuardianAngel"),
    };

    private readonly FSMentorLedger _ledger = new();
    private readonly Dictionary<(Guid Mentor, Guid Rookie), TimeSpan> _lastRevive = new();
    private readonly Dictionary<(Guid Mentor, Guid Rookie), float> _healBank = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAllyRevivedEvent>(OnRevived);
        SubscribeLocalEvent<FSAllyHealedEvent>(OnHealed);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<FSRookieFirstRoundEvent>(OnRookieFirstRound);
        SubscribeLocalEvent<FSPlayerBadgeComponent, DamageDealtEvent>(OnRookieDamaged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetRound());
    }

    private void ResetRound()
    {
        _ledger.Reset();
        _lastRevive.Clear();
        _healBank.Clear();
    }

    public bool IsEligibleMentor(ICommonSession session)
    {
        var (playtime, level, prestige) = MentorStanding(session);
        return FSMentorRules.IsEligible(playtime, level, prestige);
    }

    public (TimeSpan Playtime, int Level, int Prestige) MentorStanding(ICommonSession session)
    {
        var playtime = _playTime.TryGetTrackerTimes(session, out var times)
            ? times.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall)
            : TimeSpan.Zero;

        if (_mind.TryGetMind(session, out var mindId, out _) && TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
            return (playtime, lvl.Level, lvl.PrestigeLevel);

        var record = _store.GetFullRecord(session.UserId.UserId);
        return (playtime, record.Level, record.PrestigeLevel);
    }

    private bool TryGetPair(EntityUid mentorMind, EntityUid rookieBody,
        out ICommonSession mentor, out ICommonSession rookie)
    {
        mentor = default!;
        rookie = default!;

        if (!TryComp<MindComponent>(mentorMind, out var mind) || mind.UserId is not { } mentorId
            || !_playerManager.TryGetSessionById(mentorId, out var mentorSession))
            return false;

        if (!_mind.TryGetMind(rookieBody, out _, out var rookieMind) || rookieMind.UserId is not { } rookieId
            || !_playerManager.TryGetSessionById(rookieId, out var rookieSession))
            return false;

        mentor = mentorSession;
        rookie = rookieSession;
        return CanMentor(mentor, rookie);
    }

    public bool CanMentor(ICommonSession mentor, ICommonSession rookie)
    {
        if (mentor.UserId == rookie.UserId || !IsEligibleMentor(mentor) || !_onboarding.IsRookie(rookie))
            return false;

        return !SameConnection(mentor, rookie);
    }

    private static bool SameConnection(ICommonSession a, ICommonSession b)
    {
        var addressA = a.Channel?.RemoteEndPoint.Address;
        var addressB = b.Channel?.RemoteEndPoint.Address;
        return addressA != null && addressB != null && addressA.Equals(addressB) && !System.Net.IPAddress.IsLoopback(addressA);
    }

    private void OnRevived(ref FSAllyRevivedEvent ev)
    {
        if (!TryGetPair(ev.HealerMind, ev.Patient, out var mentor, out var rookie))
            return;

        var key = (mentor.UserId.UserId, rookie.UserId.UserId);
        var now = _timing.CurTime;
        if (_lastRevive.TryGetValue(key, out var last) && now - last < ReviveCooldown)
            return;
        _lastRevive[key] = now;

        var granted = Award(mentor, rookie, ReviveMp, "fs-mentor-reason-revive");
        if (granted > 0)
            _wallet.GiveCredits(ev.HealerMind, ReviveCredits * granted / ReviveMp);
    }

    private void OnHealed(ref FSAllyHealedEvent ev)
    {
        if (ev.Amount <= 0f || !TryGetPair(ev.HealerMind, ev.Patient, out var mentor, out var rookie))
            return;

        var key = (mentor.UserId.UserId, rookie.UserId.UserId);
        var bank = _healBank.GetValueOrDefault(key);
        var points = FSMentorRules.HealPoints(ref bank, ev.Amount, HealPerMp);
        _healBank[key] = bank;
        if (points <= 0)
            return;

        var granted = Award(mentor, rookie, points, "fs-mentor-reason-heal");
        if (granted > 0)
            _wallet.GiveCredits(ev.HealerMind, HealCreditsPerMp * granted);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var sessions = _playerManager.Sessions.Where(s => s.Status == SessionStatus.InGame && s.AttachedEntity != null).ToList();
        var rookies = sessions.Where(s => _onboarding.IsRookie(s) && _mobState.IsAlive(s.AttachedEntity!.Value)).ToList();
        if (rookies.Count == 0)
            return;

        foreach (var mentor in sessions)
        {
            if (!_mobState.IsAlive(mentor.AttachedEntity!.Value) || !IsEligibleMentor(mentor))
                continue;

            var mentorPos = _transform.GetMapCoordinates(mentor.AttachedEntity!.Value);
            foreach (var rookie in rookies)
            {
                if (!CanMentor(mentor, rookie))
                    continue;

                var rookiePos = _transform.GetMapCoordinates(rookie.AttachedEntity!.Value);
                if (rookiePos.MapId != mentorPos.MapId || (rookiePos.Position - mentorPos.Position).Length() > WaveEscortRange)
                    continue;

                Award(mentor, rookie, WaveEscortMp, "fs-mentor-reason-escort");
            }
        }
    }

    private void OnRookieFirstRound(ref FSRookieFirstRoundEvent ev)
    {
        if (!_playerManager.TryGetSessionById(ev.Rookie, out var rookie))
            return;

        foreach (var mentor in _playerManager.Sessions)
        {
            if (_presence.GetWavesPresent(mentor.UserId) > 0 && CanMentor(mentor, rookie))
                Award(mentor, rookie, FirstRoundMp, "fs-mentor-reason-first-round");
        }
    }

    private void OnRookieDamaged(Entity<FSPlayerBadgeComponent> ent, ref DamageDealtEvent args)
    {
        if (!ent.Comp.Rookie || args.Origin is not { } origin || args.Damage.GetTotal() <= 0)
            return;
        if (!_mind.TryGetMind(origin, out _, out var attackerMind) || attackerMind.UserId is not { } attacker)
            return;
        if (!_mind.TryGetMind(ent.Owner, out _, out var rookieMind) || rookieMind.UserId is not { } rookie || rookie == attacker)
            return;

        _ledger.Void(attacker.UserId, rookie.UserId);
    }

    public int Award(ICommonSession mentor, ICommonSession rookie, int amount, string reasonLocId)
    {
        var record = CurrentRecord(mentor.UserId.UserId);
        var granted = _ledger.Grant(mentor.UserId.UserId, rookie.UserId.UserId, amount, record.WeekPoints);
        if (granted <= 0)
            return 0;

        _store.SaveMentor(mentor.UserId.UserId, record with
        {
            Total = record.Total + granted,
            Balance = record.Balance + granted,
            WeekPoints = record.WeekPoints + granted,
        });

        _chat.DispatchServerMessage(mentor, Loc.GetString("fs-mentor-earned",
            ("points", granted), ("reason", Loc.GetString(reasonLocId, ("rookie", rookie.Name)))));
        return granted;
    }

    public FSPlayerDataStore.MentorRecord CurrentRecord(Guid user)
    {
        var record = _store.GetMentor(user);
        var week = FSWeeklyRules.WeekKey(DateTime.UtcNow);
        return record.WeekKey == week ? record : record with { WeekKey = week, WeekPoints = 0 };
    }

    public bool TryBuy(NetUserId user, string itemId, out string message)
    {
        var item = Shop.FirstOrDefault(i => i.Id.Equals(itemId, StringComparison.OrdinalIgnoreCase));
        if (item.Id == null)
        {
            message = Loc.GetString("fs-mentor-shop-unknown", ("item", itemId));
            return false;
        }

        if (_titles.GetOwned(user).Any(t => t.Id == item.Title))
        {
            message = Loc.GetString("fs-mentor-shop-owned");
            return false;
        }

        var record = CurrentRecord(user.UserId);
        if (record.Balance < item.Cost)
        {
            message = Loc.GetString("fs-mentor-shop-short", ("cost", item.Cost), ("balance", record.Balance));
            return false;
        }

        _store.SaveMentor(user.UserId, record with { Balance = record.Balance - item.Cost });
        _titles.Grant(user, item.Title);
        message = Loc.GetString("fs-mentor-shop-bought", ("cost", item.Cost));
        return true;
    }
}
