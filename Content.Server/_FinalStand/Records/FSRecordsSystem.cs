using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Onboarding;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._FinalStand.Records;

public sealed partial class FSRecordsSystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSLevelingSystem _leveling = default!;
    [Dependency] private FSMedicalStatsSystem _medicalStats = default!;
    [Dependency] private FSRoundPresenceSystem _presence = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    public const string VictoryTitle = "FSTitleStationHeld";
    public const string HardVictoryTitle = "FSTitleIronWall";
    public const string NightmareVictoryTitle = "FSTitleNightmareBreaker";
    public const string EndlessTitle = "FSTitleBeyondTheWall";
    public const int EndlessTitleWave = 40;

    public enum RecordKind : byte
    {
        FurthestWave,
        RoundKills,
    }

    public readonly record struct NewBest(RecordKind Kind, int Previous, int Current);

    public bool VictoryThisRound { get; set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => VictoryThisRound = false);
        SubscribeLocalEvent<FSRoundVictoryEvent>(OnVictory);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        if (args.WaveNumber < EndlessTitleWave || !VictoryThisRound)
            return;

        foreach (var session in _playerManager.Sessions)
        {
            if (_presence.GetWavesPresent(session.UserId) > 0)
                _titles.Grant(session.UserId, EndlessTitle);
        }
    }

    private void OnVictory(ref FSRoundVictoryEvent args)
    {
        VictoryThisRound = true;
        foreach (var session in _playerManager.Sessions)
        {
            if (_presence.GetWavesPresent(session.UserId) <= 0)
                continue;

            _titles.Grant(session.UserId, VictoryTitle);
            if (args.Difficulty >= FSDifficulty.Hard)
                _titles.Grant(session.UserId, HardVictoryTitle);
            if (args.Difficulty >= FSDifficulty.Nightmare)
                _titles.Grant(session.UserId, NightmareVictoryTitle);
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent args)
    {
        var rounds = new Dictionary<NetUserId, (string? Name, int Kills, int Revives)>();
        var query = EntityQueryEnumerator<FSPlayerLevelComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out _, out var mind))
        {
            if (mind.UserId is not { } userId)
                continue;

            var current = rounds.GetValueOrDefault(userId);
            rounds[userId] = (
                mind.CharacterName ?? current.Name,
                current.Kills + _leveling.GetRoundStats(mindId).Kills,
                current.Revives + _medicalStats.GetStats(mindId).Revives);
        }

        var lines = new List<string>();
        foreach (var (userId, round) in rounds)
        {
            var highest = _presence.GetHighestWavePresent(userId);
            if (highest <= 0)
                continue;

            foreach (var best in RecordRound(userId, round.Kills, round.Revives, highest, VictoryThisRound))
            {
                var text = DescribeBest(best);
                lines.Add(Loc.GetString("fs-records-round-end-line", ("name", round.Name ?? "Unknown"), ("record", text)));
                if (_playerManager.TryGetSessionById(userId, out var session))
                    _chat.DispatchServerMessage(session, Loc.GetString("fs-records-new-best", ("record", text)));
            }
        }

        if (lines.Count == 0)
            return;

        args.AddLine(Loc.GetString("fs-records-round-end-header"));
        foreach (var line in lines)
            args.AddLine(line);
    }

    public List<NewBest> RecordRound(NetUserId userId, int kills, int revives, int highestWave, bool victory)
    {
        var old = _store.GetProfile(userId.UserId);
        var updated = old with
        {
            FurthestWave = Math.Max(old.FurthestWave, highestWave),
            BestRoundKills = Math.Max(old.BestRoundKills, kills),
            TotalKills = old.TotalKills + kills,
            TotalRevives = old.TotalRevives + revives,
            RoundsPlayed = old.RoundsPlayed + 1,
            Victories = old.Victories + (victory ? 1 : 0),
        };
        _store.SaveProfile(userId.UserId, updated);

        var bests = new List<NewBest>();
        if (old.RoundsPlayed == 0)
            return bests;

        if (updated.FurthestWave > old.FurthestWave)
            bests.Add(new NewBest(RecordKind.FurthestWave, old.FurthestWave, updated.FurthestWave));
        if (updated.BestRoundKills > old.BestRoundKills)
            bests.Add(new NewBest(RecordKind.RoundKills, old.BestRoundKills, updated.BestRoundKills));
        return bests;
    }

    public FSPlayerDataStore.ProfileRecord GetProfile(NetUserId userId) => _store.GetProfile(userId.UserId);

    private string DescribeBest(NewBest best) => best.Kind switch
    {
        RecordKind.FurthestWave => Loc.GetString("fs-records-best-wave", ("current", best.Current), ("previous", best.Previous)),
        RecordKind.RoundKills => Loc.GetString("fs-records-best-kills", ("current", best.Current), ("previous", best.Previous)),
        _ => string.Empty,
    };
}
