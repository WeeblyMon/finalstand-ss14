using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.Perks;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSPerkMasterySystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;

    public const string PerkMasterTitle = "FSTitlePerkMaster";

    private readonly Dictionary<NetUserId, Dictionary<string, int>> _cache = new();
    private readonly HashSet<(NetUserId User, string Perk)> _dirty = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnEnemyDied);
        SubscribeLocalEvent<WaveEndedEvent>((ref WaveEndedEvent _) => Flush());
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            Flush();
            _cache.Clear();
        });
        SubscribeLocalEvent<PlayerDetachedEvent>(_ => Flush());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Flush();
    }

    public IReadOnlyDictionary<string, int> GetMastery(NetUserId user) => Load(user);

    private Dictionary<string, int> Load(NetUserId user)
    {
        if (!_cache.TryGetValue(user, out var kills))
            _cache[user] = kills = _store.GetMastery(user.UserId);
        return kills;
    }

    private void OnEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (args.Killer is not { } killer || !_mind.TryGetMind(killer, out var mindId, out var mind) || mind.UserId is not { } user)
            return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        foreach (var perk in perks.Slots)
        {
            if (string.IsNullOrEmpty(perk) || perks.GetSlottedLevel(perk) < FSPerkDef.MaxLevel)
                continue;

            AddKills(user, perk, 1);
        }
    }

    public void AddKills(NetUserId user, string perk, int amount)
    {
        if (amount <= 0 || !FSPerkDef.All.TryGetValue(perk, out var def))
            return;

        var kills = Load(user);
        var before = kills.GetValueOrDefault(perk);
        var after = before + amount;
        kills[perk] = after;
        _dirty.Add((user, perk));

        var oldTier = FSPerkMastery.Tier(before);
        var newTier = FSPerkMastery.Tier(after);
        if (newTier == oldTier)
            return;

        if (_playerManager.TryGetSessionById(user, out var session))
        {
            _chat.DispatchServerMessage(session, Loc.GetString("fs-mastery-tier-up",
                ("perk", def.Name), ("tier", Loc.GetString(FSPerkMastery.TierLocId(newTier)))));
        }

        if (newTier < FSMasteryTier.Gold)
            return;

        _titles.Grant(user, PerkMasterTitle);

        var category = FSPerkDef.All.Values.Where(p => p.Category == def.Category).ToList();
        if (category.All(p => FSPerkMastery.Tier(kills.GetValueOrDefault(p.Id)) == FSMasteryTier.Gold))
            _titles.Grant(user, FSPerkMastery.CategoryTitle(def.Category));
    }

    public void Flush()
    {
        if (_dirty.Count == 0)
            return;

        var pending = _dirty.ToList();
        _dirty.Clear();
        _store.RunBatch("mastery flush", () =>
        {
            foreach (var (user, perk) in pending)
            {
                if (_cache.TryGetValue(user, out var kills))
                    _store.SaveMastery(user.UserId, perk, kills.GetValueOrDefault(perk));
            }
        });
    }
}
