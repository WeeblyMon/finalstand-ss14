using System.Linq;
using System.Text.Json;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
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

    private readonly Dictionary<ICommonSession, TimeSpan> _stateRequestCooldown = new();
    private static readonly TimeSpan StateRequestInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<FSPerkStateRequestMessage>(OnStateRequested);
        SubscribeNetworkEvent<FSBuyPerkMessage>(OnBuyPerk);
        SubscribeNetworkEvent<FSEquipPerkMessage>(OnEquipPerk);
        SubscribeNetworkEvent<FSUnequipPerkMessage>(OnUnequipPerk);
        SubscribeNetworkEvent<FSSaveLoadoutMessage>(OnSaveLoadout);
        SubscribeNetworkEvent<FSLoadLoadoutMessage>(OnLoadLoadout);
        SubscribeNetworkEvent<FSRespecPerkMessage>(OnRespec);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind))
        {
            Log.Debug($"[FSPerk] OnPlayerAttached: TryGetMind failed for entity={ev.Entity} — skipping ECS component setup");
            return;
        }
        if (mind.UserId == null)
        {
            Log.Debug($"[FSPerk] OnPlayerAttached: mind={mindId} has no UserId — skipping");
            return;
        }

        var (levelsJson, slotsJson, loadoutsJson) = _store.LoadPerkLoadout(mind.UserId.Value.UserId);
        var perks = EnsureComp<FSPerkLevelsComponent>(mindId);
        DeserializeInto(perks, levelsJson, slotsJson, loadoutsJson);
        Log.Debug($"[FSPerk] OnPlayerAttached: added FSPerkLevelsComponent to mind={mindId} for entity={ev.Entity}");

        SendStateToClient(mindId, perks, mind.UserId.Value.UserId);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind)) return;

        if (mind?.UserId != null &&
            _playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            _stateRequestCooldown.Remove(session);

        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks)) return;
        SaveToDb(mindId, perks);
    }

    private void OnStateRequested(FSPerkStateRequestMessage msg, EntitySessionEventArgs args)
    {
        var now = _timing.CurTime;
        if (_stateRequestCooldown.TryGetValue(args.SenderSession, out var last) &&
            now - last < StateRequestInterval)
            return;
        _stateRequestCooldown[args.SenderSession] = now;

        if (_mind.TryGetMind(args.SenderSession, out var mindId, out MindComponent? mind))
        {
            var perks = EnsurePerkComponent(mindId, mind);
            if (perks != null)
            {
                SendStateToClient(mindId, perks);
                return;
            }
        }

        // lobby: no mind yet — load from db and send so the shop window populates
        var userId = args.SenderSession.UserId.UserId;
        var (levelsJson, slotsJson, loadoutsJson) = _store.LoadPerkLoadout(userId);
        var tempPerks = new FSPerkLevelsComponent();
        DeserializeInto(tempPerks, levelsJson, slotsJson, loadoutsJson);
        var ap = _wallet.GetStoredPerkPoints(userId);

        SendState(args.SenderSession, tempPerks, ap);
    }

    private void OnBuyPerk(FSBuyPerkMessage msg, EntitySessionEventArgs args)
    {
        if (!FSPerkDef.All.ContainsKey(msg.PerkId))
        {
            Log.Debug($"[FSPerk] OnBuyPerk: unknown perk id '{msg.PerkId}'");
            return;
        }

        var session = args.SenderSession;
        var available = GetPerkPoints(session);

        DispatchPerkMutation(session, perks =>
        {
            var currentLevel = perks.GetLevel(msg.PerkId);
            if (currentLevel >= FSPerkDef.MaxLevel)
                return false;

            var cost = FSPerkDef.CostForUpgrade(currentLevel);
            if (available < cost)
                return false;

            // GivePerkPoints bridges lobby/in-round, so this call is correct either way.
            _wallet.GivePerkPoints(session, -cost);
            perks.Levels[msg.PerkId] = currentLevel + 1;
            return true;
        });
    }

    private void OnEquipPerk(FSEquipPerkMessage msg, EntitySessionEventArgs args)
    {
        if (!FSPerkDef.All.ContainsKey(msg.PerkId)) return;
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
            perks.Slots[msg.SlotIndex] = string.Empty;
            return true;
        });
    }

    private void OnSaveLoadout(FSSaveLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        DispatchPerkMutation(args.SenderSession, perks =>
        {
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
        var points = GetPerkPoints(session);

        DispatchPerkMutation(session, perks =>
        {
            var src = perks.Loadouts[msg.LoadoutIndex];
            if (src.IsEmpty)
                return false;

            var refund = CalcRefund(perks);
            var cost = CalcCost(src.Levels);
            if (points + refund < cost)
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
        if (_mind.TryGetMind(session, out var mindId, out _)
            && TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            return wallet.PerkPoints;

        return _wallet.GetStoredPerkPoints(session.UserId.UserId);
    }

    private static int CalcRefund(FSPerkLevelsComponent perks) => CalcCost(perks.Levels);

    private static int CalcCost(Dictionary<string, int> levels)
    {
        var total = 0;
        foreach (var (_, level) in levels)
            for (var i = 0; i < level; i++)
                total += FSPerkDef.CostForUpgrade(i);
        return total;
    }

    // Handles both in-round (mind entity) and lobby (DB-direct) cases transparently.
    private void DispatchPerkMutation(ICommonSession session, Func<FSPerkLevelsComponent, bool> mutate)
    {
        if (_mind.TryGetMind(session, out var mindId, out var mind))
        {
            // In-round path: operate on the ECS component, creating it lazily if OnPlayerAttached missed it.
            var perks = EnsurePerkComponent(mindId, mind);
            if (perks == null) return;
            if (!mutate(perks)) return;
            perks.Invalidate();
            SaveToDb(mindId, perks);
            SendStateToClient(mindId, perks);
            if (mind?.CurrentEntity is { } playerEntity)
                _movement.RefreshMovementSpeedModifiers(playerEntity);
            return;
        }

        // Lobby path: no mind yet — operate on DB directly
        var userId = session.UserId.UserId;
        var (lj, sj, oj) = _store.LoadPerkLoadout(userId);
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
        if (TryComp<FSPerkLevelsComponent>(mindId, out var existing))
            return existing;

        if (mind?.UserId == null)
            return null;

        var (lj, sj, oj) = _store.LoadPerkLoadout(mind.UserId.Value.UserId);
        var perks = EnsureComp<FSPerkLevelsComponent>(mindId);
        DeserializeInto(perks, lj, sj, oj);
        Log.Debug($"[FSPerk] EnsurePerkComponent: lazily created FSPerkLevelsComponent on mind={mindId}");
        return perks;
    }

    private void SaveToDb(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
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

        int ap;
        if (TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            ap = wallet.PerkPoints;
        else
            ap = _wallet.GetStoredPerkPoints(mind.UserId.Value.UserId);

        SendState(session, perks, ap);
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

    private const int MaxJsonBytes = 65_536; // 64 KB — any legitimate perk payload is <1 KB

    private void DeserializeInto(FSPerkLevelsComponent perks,
        string levelsJson, string slotsJson, string loadoutsJson)
    {
        perks.Invalidate();

        if (levelsJson.Length > MaxJsonBytes || slotsJson.Length > MaxJsonBytes || loadoutsJson.Length > MaxJsonBytes)
        {
            Log.Error($"[FSPerk] Oversized JSON in perk data (levels={levelsJson.Length} slots={slotsJson.Length} loadouts={loadoutsJson.Length}) — discarding all.");
            return;
        }

        if (!string.IsNullOrEmpty(levelsJson))
        {
            try
            {
                var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson);
                if (levels != null)
                {
                    foreach (var key in levels.Keys.Where(k => !FSPerkDef.All.ContainsKey(k)).ToList())
                        levels.Remove(key);
                    perks.Levels = levels;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSPerk] Failed to deserialize perk levels JSON: {e.Message}\nJSON was: {levelsJson}");
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
                            Log.Warning($"[FSPerk] Slot {i} contained invalid/unowned perk '{slots[i]}' — clearing.");
                            slots[i] = string.Empty;
                        }
                    }
                    perks.Slots = slots;
                }
                else if (slots != null)
                {
                    Log.Warning($"[FSPerk] Slot JSON had {slots.Length} entries but SlotCount is {FSPerkDef.SlotCount} — discarding slots (SlotCount may have changed).");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSPerk] Failed to deserialize perk slots JSON: {e.Message}\nJSON was: {slotsJson}");
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
                            Log.Warning($"[FSPerk] Loadout {i} had {loadout.Slots.Length} slots — discarding (SlotCount mismatch).");
                            continue;
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
                Log.Error($"[FSPerk] Failed to deserialize perk loadouts JSON: {e.Message}\nJSON was: {loadoutsJson}");
            }
        }
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

            Log.Info("[FSPerk] Migrated legacy slot-only loadouts; levels were not stored in that format.");
        }
        catch (Exception e)
        {
            Log.Error($"[FSPerk] Failed to deserialize legacy perk loadouts JSON: {e.Message}");
        }
    }
}
