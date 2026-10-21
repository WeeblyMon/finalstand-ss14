using System.Linq;
using System.Text.Json;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSPerkSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private FSPerkShopAccessSystem _shopAccess = default!;

    private readonly Dictionary<NetUserId, TimeSpan> _stateRequestCooldown = new();
    private static readonly TimeSpan StateRequestInterval = TimeSpan.FromSeconds(1);

    // Every perk change is a database write. A burst covers fast clicking, then one change per interval.
    private const int MutationBurst = 8;
    private static readonly TimeSpan MutationInterval = TimeSpan.FromSeconds(0.25);
    private readonly Dictionary<NetUserId, TimeSpan> _mutationHorizon = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<FSPrestigeWipedEvent>(OnPrestigeWiped);
        SubscribeNetworkEvent<FSPerkStateRequestMessage>(OnStateRequested);
        SubscribeNetworkEvent<FSBuyPerkMessage>(OnBuyPerk);
        SubscribeNetworkEvent<FSEquipPerkMessage>(OnEquipPerk);
        SubscribeNetworkEvent<FSUnequipPerkMessage>(OnUnequipPerk);
        SubscribeNetworkEvent<FSSaveLoadoutMessage>(OnSaveLoadout);
        SubscribeNetworkEvent<FSLoadLoadoutMessage>(OnLoadLoadout);
        SubscribeNetworkEvent<FSRespecPerkMessage>(OnRespec);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    // Lobby sessions never detach, and a new connection is a new session; drop the throttles with the connection.
    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        _stateRequestCooldown.Remove(args.Session.UserId);
        _mutationHorizon.Remove(args.Session.UserId);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind))
        {
            Log.Debug($"OnPlayerAttached: TryGetMind failed for entity={ev.Entity} — skipping ECS component setup");
            return;
        }
        if (mind.UserId == null)
        {
            Log.Debug($"OnPlayerAttached: mind={mindId} has no UserId — skipping");
            return;
        }

        var perks = LoadPerks(mindId, mind.UserId.Value.UserId);
        Log.Debug($"OnPlayerAttached: added FSPerkLevelsComponent to mind={mindId} for entity={ev.Entity}");

        SendStateToClient(mindId, perks, mind.UserId.Value.UserId);
        RaisePerksChanged(ev.Entity, mindId, perks);
    }

    // The rows are already gone; clear the in-memory copy too or the next save writes it all back.
    private void OnPrestigeWiped(ref FSPrestigeWipedEvent args)
    {
        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var perks, out var mind))
        {
            perks.Levels.Clear();
            Array.Fill(perks.Slots, string.Empty);
            for (var i = 0; i < perks.Loadouts.Length; i++)
                perks.Loadouts[i] = new FSPerkLoadout();
            perks.Invalidate();

            SendStateToClient(mindId, perks);
            if (mind.CurrentEntity is { } body)
            {
                _movement.RefreshMovementSpeedModifiers(body);
                RaisePerksChanged(body, mindId, perks);
            }
        }
    }

    private void RaisePerksChanged(EntityUid body, EntityUid mindId, FSPerkLevelsComponent perks)
    {
        var changed = new FSPerksChangedEvent(mindId, perks);
        RaiseLocalEvent(body, ref changed);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out var mind) || mind.UserId == null)
            return;

        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks) || !perks.Loaded)
        {
            perks = LoadPerks(mindId, mind.UserId.Value.UserId);
            SendStateToClient(mindId, perks, mind.UserId.Value.UserId);
        }

        RaisePerksChanged(ev.Mob, mindId, perks);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        _stateRequestCooldown.Remove(ev.Player.UserId);

        if (!_mind.TryGetMind(ev.Entity, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks)) return;
        SaveToDb(mindId, perks);
    }

    private void OnStateRequested(FSPerkStateRequestMessage msg, EntitySessionEventArgs args)
    {
        var now = _timing.CurTime;
        if (_stateRequestCooldown.TryGetValue(args.SenderSession.UserId, out var last) &&
            now - last < StateRequestInterval)
            return;
        _stateRequestCooldown[args.SenderSession.UserId] = now;

        if (_mind.TryGetMind(args.SenderSession, out var mindId, out MindComponent? mind))
        {
            var perks = EnsurePerkComponent(mindId, mind);
            if (perks != null)
            {
                SendStateToClient(mindId, perks);
                return;
            }
        }

        // lobby: no mind yet; load from db and send so the shop window populates
        var userId = args.SenderSession.UserId.UserId;
        var (levelsJson, slotsJson, loadoutsJson) = _store.LoadPerkLoadout(userId);
        var tempPerks = new FSPerkLevelsComponent();
        DeserializeInto(tempPerks, levelsJson, slotsJson, loadoutsJson);
        var ap = _wallet.GetStoredPerkPoints(userId);

        SendState(args.SenderSession, tempPerks, ap);
    }

    private void OnBuyPerk(FSBuyPerkMessage msg, EntitySessionEventArgs args)
    {
        if (msg.PerkId is null || !FSPerkDef.All.ContainsKey(msg.PerkId))
        {
            Log.Debug($"OnBuyPerk: unknown perk id '{msg.PerkId}'");
            return;
        }

        var session = args.SenderSession;

        DispatchPerkMutation(session, perks =>
        {
            var currentLevel = perks.GetLevel(msg.PerkId);
            if (currentLevel >= FSPerkDef.MaxLevel)
                return false;

            var cost = FSPerkDef.CostForUpgrade(currentLevel);
            if (GetPerkPoints(session) < cost)
                return false;

            // GivePerkPoints bridges lobby/in-round, so this call is correct either way.
            _wallet.GivePerkPoints(session, -cost);
            perks.Levels[msg.PerkId] = currentLevel + 1;
            return true;
        });
    }

    private void OnEquipPerk(FSEquipPerkMessage msg, EntitySessionEventArgs args)
    {
        if (msg.PerkId is null || !FSPerkDef.All.ContainsKey(msg.PerkId)) return;
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSPerkDef.SlotCount) return;

        DispatchPerkMutation(args.SenderSession, perks =>
        {
            if (perks.GetLevel(msg.PerkId) <= 0) return false;
            if (!string.IsNullOrEmpty(perks.Slots[msg.SlotIndex])) return false;
            if (perks.Slots.Contains(msg.PerkId)) return false;
            perks.Slots[msg.SlotIndex] = msg.PerkId;
            return true;
        });
    }

    private void OnUnequipPerk(FSUnequipPerkMessage msg, EntitySessionEventArgs args)
    {
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSPerkDef.SlotCount) return;

        DispatchPerkMutation(args.SenderSession, perks =>
        {
            if (string.IsNullOrEmpty(perks.Slots[msg.SlotIndex]))
                return false;

            perks.Slots[msg.SlotIndex] = string.Empty;
            return true;
        });
    }

    private void OnSaveLoadout(FSSaveLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        DispatchPerkMutation(args.SenderSession, perks =>
        {
            // An empty build would silently erase the saved loadout; an unchanged one is a wasted write.
            if (perks.Levels.Count == 0 || MatchesBuild(perks, perks.Loadouts[msg.LoadoutIndex]))
                return false;

            var loadout = new FSPerkLoadout { Levels = new Dictionary<string, int>(perks.Levels) };
            Array.Copy(perks.Slots, loadout.Slots, FSPerkDef.SlotCount);
            perks.Loadouts[msg.LoadoutIndex] = loadout;
            return true;
        });
    }

    // Loading re-buys the saved build: the current one is refunded first, so the player only pays
    // the difference. Refuses outright rather than applying a partial build.
    private void OnLoadLoadout(FSLoadLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        var session = args.SenderSession;

        DispatchPerkMutation(session, perks =>
        {
            var src = perks.Loadouts[msg.LoadoutIndex];
            if (src.IsEmpty || MatchesBuild(perks, src))
                return false;

            var refund = CalcRefund(perks);
            var cost = CalcCost(src.Levels);
            if (GetPerkPoints(session) + refund < cost)
                return false;

            _wallet.GivePerkPoints(session, refund - cost);

            perks.Levels = new Dictionary<string, int>(src.Levels);
            for (var i = 0; i < FSPerkDef.SlotCount; i++)
            {
                var id = src.Slots[i];
                perks.Slots[i] = !string.IsNullOrEmpty(id) && perks.GetLevel(id) > 0 ? id : string.Empty;
            }
            return true;
        });
    }

    private void OnRespec(FSRespecPerkMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        DispatchPerkMutation(session, perks =>
        {
            if (perks.Levels.Count == 0)
                return false;

            _wallet.GivePerkPoints(session, CalcRefund(perks));
            perks.Levels.Clear();
            Array.Fill(perks.Slots, string.Empty);
            return true;
        });
    }

    // Live component in round, database row in the lobby.
    private int GetPerkPoints(ICommonSession session)
    {
        return _wallet.GetStoredPerkPoints(session.UserId.UserId);
    }

    private static int CalcRefund(FSPerkLevelsComponent perks) => CalcCost(perks.Levels);

    private static bool MatchesBuild(FSPerkLevelsComponent perks, FSPerkLoadout loadout)
    {
        if (perks.Levels.Count != loadout.Levels.Count || !perks.Slots.SequenceEqual(loadout.Slots))
            return false;

        foreach (var (id, level) in perks.Levels)
        {
            if (loadout.Levels.GetValueOrDefault(id) != level)
                return false;
        }

        return true;
    }

    private static int CalcCost(Dictionary<string, int> levels)
    {
        var total = 0;
        foreach (var (_, level) in levels)
            for (var i = 0; i < level; i++)
                total += FSPerkDef.CostForUpgrade(i);
        return total;
    }

    // Each accepted change pushes the user's horizon out one interval; once it's a full burst ahead, changes wait.
    private bool TryTakeMutation(NetUserId user)
    {
        var now = _timing.CurTime;
        var horizon = _mutationHorizon.TryGetValue(user, out var stored) && stored > now ? stored : now;
        if (horizon - now >= MutationInterval * MutationBurst)
            return false;

        _mutationHorizon[user] = horizon + MutationInterval;
        return true;
    }

    private void DispatchPerkMutation(ICommonSession session, Func<FSPerkLevelsComponent, bool> mutate)
    {
        if (!TryTakeMutation(session.UserId))
            return;

        if (session.AttachedEntity is { } attached && !_shopAccess.CanUse(attached))
            return;

        if (_mind.TryGetMind(session, out var mindId, out var mind))
        {
            // In-round path: operate on the ECS component, creating it lazily if OnPlayerAttached missed it.
            var perks = EnsurePerkComponent(mindId, mind);
            // A build that couldn't be read can't be saved, and a purchase would still spend the perk points.
            if (perks is not { Loaded: true }) return;
            if (!mutate(perks)) return;
            perks.Invalidate();
            SaveToDb(mindId, perks);
            SendStateToClient(mindId, perks);
            if (mind?.CurrentEntity is { } playerEntity)
            {
                _movement.RefreshMovementSpeedModifiers(playerEntity);
                RaisePerksChanged(playerEntity, mindId, perks);
            }
            return;
        }

        // Lobby path: no mind yet, so operate on the DB directly. A failed read would save the change over an
        // empty build.
        var userId = session.UserId.UserId;
        if (!_store.TryLoadPerkLoadout(userId, out var lobbyLoadout))
            return;

        var (lj, sj, oj) = lobbyLoadout;
        var lobbyPerks = new FSPerkLevelsComponent();
        DeserializeInto(lobbyPerks, lj, sj, oj);

        if (!mutate(lobbyPerks)) return;

        _store.SaveLoadoutJson(userId,
            JsonSerializer.Serialize(lobbyPerks.Levels),
            JsonSerializer.Serialize(lobbyPerks.Slots),
            JsonSerializer.Serialize(lobbyPerks.Loadouts));

        if (!_playerManager.TryGetSessionById(session.UserId, out var pSession)) return;

        SendState(pSession, lobbyPerks, _wallet.GetStoredPerkPoints(userId));
    }

    // Returns null if the mind has no UserId (can't load data).
    private FSPerkLevelsComponent? EnsurePerkComponent(EntityUid mindId, MindComponent? mind)
    {
        if (TryComp<FSPerkLevelsComponent>(mindId, out var existing) && existing.Loaded)
            return existing;

        if (mind?.UserId == null)
            return existing;

        var perks = LoadPerks(mindId, mind.UserId.Value.UserId);
        Log.Debug($"EnsurePerkComponent: lazily created FSPerkLevelsComponent on mind={mindId}");
        return perks;
    }

    // Reads the saved build into the mind's perks. A failed read leaves whatever the component already had.
    private FSPerkLevelsComponent LoadPerks(EntityUid mindId, Guid userId)
    {
        var perks = EnsureComp<FSPerkLevelsComponent>(mindId);
        if (!_store.TryLoadPerkLoadout(userId, out var loadout))
            return perks;

        DeserializeInto(perks, loadout.LevelsJson, loadout.SlotsJson, loadout.LoadoutsJson);
        perks.Loaded = true;
        return perks;
    }

    private void SaveToDb(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        if (!perks.Loaded || !TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;

        _store.SaveLoadoutJson(mind.UserId.Value.UserId,
            JsonSerializer.Serialize(perks.Levels),
            JsonSerializer.Serialize(perks.Slots),
            JsonSerializer.Serialize(perks.Loadouts));
    }

    private void SendStateToClient(EntityUid mindId, FSPerkLevelsComponent perks, Guid? userId = null)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session)) return;

        SendState(session, perks, _wallet.GetPerkPoints(mindId, mind.UserId.Value));
    }

    // The payload is deep-copied: the component's arrays keep mutating after this returns.
    private void SendState(ICommonSession session, FSPerkLevelsComponent perks, int perkPoints)
    {
        RaiseNetworkEvent(new FSPerksStateEvent
        {
            PerkPoints = perkPoints,
            Levels = new Dictionary<string, int>(perks.Levels),
            Slots = (string[])perks.Slots.Clone(),
            Loadouts = perks.Loadouts,
        }, Filter.SinglePlayer(session));
    }

    private const int MaxJsonBytes = 65_536; // 64 KB; any legitimate perk payload is <1 KB

    private void DeserializeInto(FSPerkLevelsComponent perks,
        string levelsJson, string slotsJson, string loadoutsJson)
    {
        perks.Invalidate();

        if (levelsJson.Length > MaxJsonBytes || slotsJson.Length > MaxJsonBytes || loadoutsJson.Length > MaxJsonBytes)
        {
            Log.Error($"Oversized JSON in perk data (levels={levelsJson.Length} slots={slotsJson.Length} loadouts={loadoutsJson.Length}) — discarding all.");
            return;
        }

        if (!string.IsNullOrEmpty(levelsJson))
        {
            try
            {
                var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson);
                if (levels != null)
                    perks.Levels = SanitizeLevels(levels);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to deserialize perk levels JSON: {e.Message}\nJSON was: {levelsJson}");
            }
        }

        if (!string.IsNullOrEmpty(slotsJson))
        {
            try
            {
                var slots = JsonSerializer.Deserialize<string[]>(slotsJson);
                if (slots?.Length == FSPerkDef.SlotCount)
                {
                    for (var i = 0; i < FSPerkDef.SlotCount; i++)
                    {
                        if (!string.IsNullOrEmpty(slots[i]) &&
                            (!FSPerkDef.All.ContainsKey(slots[i]) || perks.GetLevel(slots[i]) <= 0))
                        {
                            Log.Warning($"Slot {i} contained invalid/unowned perk '{slots[i]}' — clearing.");
                            slots[i] = string.Empty;
                        }
                    }
                    perks.Slots = slots;
                }
                else if (slots != null)
                {
                    Log.Warning($"Slot JSON had {slots.Length} entries but SlotCount is {FSPerkDef.SlotCount} — discarding slots (SlotCount may have changed).");
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to deserialize perk slots JSON: {e.Message}\nJSON was: {slotsJson}");
            }
        }

        if (!string.IsNullOrEmpty(loadoutsJson))
        {
            try
            {
                var loadouts = JsonSerializer.Deserialize<FSPerkLoadout[]>(loadoutsJson);
                if (loadouts?.Length == 3)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        if (loadouts[i] is not { } loadout)
                            continue;

                        if (loadout.Slots.Length != FSPerkDef.SlotCount)
                        {
                            Log.Warning($"Loadout {i} had {loadout.Slots.Length} slots — discarding (SlotCount mismatch).");
                            continue;
                        }

                        loadout.Levels = SanitizeLevels(loadout.Levels ?? new());
                        for (var slot = 0; slot < loadout.Slots.Length; slot++)
                        {
                            if (loadout.Slots[slot] is not { } id || !FSPerkDef.All.ContainsKey(id))
                                loadout.Slots[slot] = string.Empty;
                        }

                        perks.Loadouts[i] = loadout;
                    }
                }
            }
            catch (JsonException)
            {
                // Rows saved before loadouts stored levels held string[][] (slots only). Keep the
                // arrangement and drop the levels that format never recorded.
                TryLoadLegacyLoadouts(loadoutsJson, perks);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to deserialize perk loadouts JSON: {e.Message}\nJSON was: {loadoutsJson}");
            }
        }
    }

    // Stored levels are only as good as the row: unknown perks and anything outside 1..MaxLevel would index past
    // the perk systems' per-level tables.
    internal static Dictionary<string, int> SanitizeLevels(Dictionary<string, int> levels)
    {
        var clean = new Dictionary<string, int>(levels.Count);
        foreach (var (id, level) in levels)
        {
            if (level >= 1 && FSPerkDef.All.ContainsKey(id))
                clean[id] = Math.Min(level, FSPerkDef.MaxLevel);
        }

        return clean;
    }

    private void TryLoadLegacyLoadouts(string loadoutsJson, FSPerkLevelsComponent perks)
    {
        try
        {
            var legacy = JsonSerializer.Deserialize<string[][]>(loadoutsJson);
            if (legacy?.Length != 3)
                return;

            for (var i = 0; i < 3; i++)
            {
                if (legacy[i]?.Length != FSPerkDef.SlotCount)
                    continue;

                var loadout = new FSPerkLoadout();
                Array.Copy(legacy[i], loadout.Slots, FSPerkDef.SlotCount);
                perks.Loadouts[i] = loadout;
            }

            Log.Info("Migrated legacy slot-only loadouts; levels were not stored in that format.");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to deserialize legacy perk loadouts JSON: {e.Message}");
        }
    }
}
