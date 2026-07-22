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

public sealed class FSPerkSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

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
        SubscribeNetworkEvent<FSUnequipAugmentMessage>(OnUnequipAugment);
        SubscribeNetworkEvent<FSSaveLoadoutMessage>(OnSaveLoadout);
        SubscribeNetworkEvent<FSLoadLoadoutMessage>(OnLoadLoadout);
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

        var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
        var aug = EnsureComp<FSPerkLevelsComponent>(mindId);
        DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);
        Log.Debug($"[FSPerk] OnPlayerAttached: added FSPerkLevelsComponent to mind={mindId} for entity={ev.Entity}");

        SendStateToClient(mindId, aug, mind.UserId.Value.UserId);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind)) return;

        if (mind?.UserId != null &&
            _playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            _stateRequestCooldown.Remove(session);

        if (!TryComp<FSPerkLevelsComponent>(mindId, out var aug)) return;
        SaveToDb(mindId, aug);
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
            var aug = EnsureAugComponent(mindId, mind);
            if (aug != null)
            {
                SendStateToClient(mindId, aug);
                return;
            }
        }

        // lobby: no mind yet — load from db and send so the shop window populates
        var userId = args.SenderSession.UserId.UserId;
        var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(userId);
        var tempAug = new FSPerkLevelsComponent();
        DeserializeInto(tempAug, levelsJson, slotsJson, loadoutsJson);
        var ap = _wallet.GetStoredPerkPoints(userId);

        RaiseNetworkEvent(new FSPerksStateEvent
        {
            PerkPoints = ap,
            Levels = new Dictionary<string, int>(tempAug.Levels),
            Slots = (string[])tempAug.Slots.Clone(),
            Loadouts = tempAug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(args.SenderSession));
    }

    private void OnBuyPerk(FSBuyPerkMessage msg, EntitySessionEventArgs args)
    {
        if (!FSPerkDef.All.ContainsKey(msg.PerkId))
        {
            Log.Debug($"[FSPerk] OnBuyPerk: unknown augment id '{msg.PerkId}'");
            return;
        }

        if (_mind.TryGetMind(args.SenderSession, out var mindId, out var mind))
        {
            OnBuyPerkInRound(msg, args.SenderSession, mindId, mind);
        }
        else
        {
            OnBuyPerkLobby(msg, args.SenderSession);
        }
    }

    private void OnBuyPerkInRound(FSBuyPerkMessage msg, ICommonSession session,
        EntityUid mindId, MindComponent? mind)
    {
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var aug))
        {
            if (mind?.UserId == null)
            {
                Log.Debug($"[FSPerk] OnBuyPerk: no UserId on mind {mindId}");
                return;
            }
            var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
            aug = EnsureComp<FSPerkLevelsComponent>(mindId);
            DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);
        }

        var currentLevel = aug.GetLevel(msg.PerkId);
        if (currentLevel >= FSPerkDef.MaxLevel)
        {
            Log.Debug($"[FSPerk] OnBuyPerk: '{msg.PerkId}' already max level ({currentLevel})");
            return;
        }

        var cost = FSPerkDef.CostForUpgrade(currentLevel);
        if (!_wallet.TryDeductPerkPoints(mindId, cost))
        {
            Log.Debug($"[FSPerk] OnBuyPerk: TryDeductPerkPoints failed — mind={mindId} cost={cost}");
            return;
        }

        aug.Levels[msg.PerkId] = currentLevel + 1;
        Log.Debug($"[FSPerk] OnBuyPerk: SUCCESS — '{msg.PerkId}' → Lv{currentLevel + 1} for {session.Name}");
        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
    }

    private void OnBuyPerkLobby(FSBuyPerkMessage msg, ICommonSession session)
    {
        var userId = session.UserId.UserId;
        var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(userId);
        var aug = new FSPerkLevelsComponent();
        DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);

        var currentLevel = aug.GetLevel(msg.PerkId);
        if (currentLevel >= FSPerkDef.MaxLevel)
        {
            Log.Debug($"[FSPerk] OnBuyPerk (lobby): '{msg.PerkId}' already max level");
            return;
        }

        var cost = FSPerkDef.CostForUpgrade(currentLevel);
        var currentAp = _wallet.GetStoredPerkPoints(userId);
        if (currentAp < cost)
        {
            Log.Debug($"[FSPerk] OnBuyPerk (lobby): insufficient AP — have {currentAp}, need {cost}");
            return;
        }

        aug.Levels[msg.PerkId] = currentLevel + 1;
        var newAp = currentAp - cost;

        _wallet.GivePerkPoints(session, -cost);
        _wallet.SaveAugmentDataByUser(userId,
            JsonSerializer.Serialize(aug.Levels),
            JsonSerializer.Serialize(aug.Slots),
            JsonSerializer.Serialize(aug.Loadouts));

        Log.Debug($"[FSPerk] OnBuyPerk (lobby): SUCCESS — '{msg.PerkId}' → Lv{currentLevel + 1} for {session.Name}");

        if (!_playerManager.TryGetSessionById(session.UserId, out var pSession))
            return;

        RaiseNetworkEvent(new WalletUpdatedEvent(0, newAp), Filter.SinglePlayer(pSession));
        RaiseNetworkEvent(new FSPerksStateEvent
        {
            PerkPoints = newAp,
            Levels = new Dictionary<string, int>(aug.Levels),
            Slots = (string[])aug.Slots.Clone(),
            Loadouts = aug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(pSession));
    }

    private void OnEquipPerk(FSEquipPerkMessage msg, EntitySessionEventArgs args)
    {
        if (!FSPerkDef.All.ContainsKey(msg.PerkId)) return;
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSPerkDef.SlotCount) return;

        DispatchAugMutation(args.SenderSession, aug =>
        {
            if (aug.GetLevel(msg.PerkId) <= 0) return false;
            if (!string.IsNullOrEmpty(aug.Slots[msg.SlotIndex])) return false;
            if (aug.Slots.Contains(msg.PerkId)) return false;
            aug.Slots[msg.SlotIndex] = msg.PerkId;
            return true;
        });
    }

    private void OnUnequipAugment(FSUnequipAugmentMessage msg, EntitySessionEventArgs args)
    {
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSPerkDef.SlotCount) return;

        DispatchAugMutation(args.SenderSession, aug =>
        {
            aug.Slots[msg.SlotIndex] = string.Empty;
            return true;
        });
    }

    private void OnSaveLoadout(FSSaveLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        DispatchAugMutation(args.SenderSession, aug =>
        {
            Array.Copy(aug.Slots, aug.Loadouts[msg.LoadoutIndex], FSPerkDef.SlotCount);
            return true;
        });
    }

    private void OnLoadLoadout(FSLoadLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        DispatchAugMutation(args.SenderSession, aug =>
        {
            var src = aug.Loadouts[msg.LoadoutIndex];
            for (var i = 0; i < FSPerkDef.SlotCount; i++)
            {
                var id = src[i];
                aug.Slots[i] = !string.IsNullOrEmpty(id) && aug.GetLevel(id) > 0 ? id : string.Empty;
            }
            return true;
        });
    }

    // Runs a mutation on a player's FSPerkLevelsComponent, saving and notifying on success.
    // Handles both in-round (mind entity) and lobby (DB-direct) cases transparently.
    private void DispatchAugMutation(ICommonSession session, Func<FSPerkLevelsComponent, bool> mutate)
    {
        if (_mind.TryGetMind(session, out var mindId, out var mind))
        {
            // In-round path: operate on the ECS component, creating it lazily if OnPlayerAttached missed it.
            var aug = EnsureAugComponent(mindId, mind);
            if (aug == null) return;
            if (!mutate(aug)) return;
            SaveToDb(mindId, aug);
            SendStateToClient(mindId, aug);
            if (mind?.CurrentEntity is { } playerEntity)
                _movement.RefreshMovementSpeedModifiers(playerEntity);
            return;
        }

        // Lobby path: no mind yet — operate on DB directly
        var userId = session.UserId.UserId;
        var (lj, sj, oj) = _wallet.LoadAugmentData(userId);
        var lobbyAug = new FSPerkLevelsComponent();
        DeserializeInto(lobbyAug, lj, sj, oj);

        if (!mutate(lobbyAug)) return;

        _wallet.SaveAugmentDataByUser(userId,
            JsonSerializer.Serialize(lobbyAug.Levels),
            JsonSerializer.Serialize(lobbyAug.Slots),
            JsonSerializer.Serialize(lobbyAug.Loadouts));

        if (!_playerManager.TryGetSessionById(session.UserId, out var pSession)) return;

        var ap = _wallet.GetStoredPerkPoints(userId);
        RaiseNetworkEvent(new FSPerksStateEvent
        {
            PerkPoints = ap,
            Levels = new Dictionary<string, int>(lobbyAug.Levels),
            Slots = (string[])lobbyAug.Slots.Clone(),
            Loadouts = lobbyAug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(pSession));
    }

    // Returns the existing FSPerkLevelsComponent on mindId, or creates one from DB if missing.
    // Returns null if the mind has no UserId (can't load data).
    private FSPerkLevelsComponent? EnsureAugComponent(EntityUid mindId, MindComponent? mind)
    {
        if (TryComp<FSPerkLevelsComponent>(mindId, out var existing))
            return existing;

        if (mind?.UserId == null)
            return null;

        var (lj, sj, oj) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
        var aug = EnsureComp<FSPerkLevelsComponent>(mindId);
        DeserializeInto(aug, lj, sj, oj);
        Log.Debug($"[FSPerk] EnsureAugComponent: lazily created FSPerkLevelsComponent on mind={mindId}");
        return aug;
    }

    private void SaveToDb(EntityUid mindId, FSPerkLevelsComponent aug)
    {
        _wallet.SaveAugmentData(mindId,
            JsonSerializer.Serialize(aug.Levels),
            JsonSerializer.Serialize(aug.Slots),
            JsonSerializer.Serialize(aug.Loadouts));
    }

    private void SendStateToClient(EntityUid mindId, FSPerkLevelsComponent aug, Guid? userId = null)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session)) return;

        int ap;
        if (TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            ap = wallet.PerkPoints;
        else
            ap = _wallet.GetStoredPerkPoints(mind.UserId.Value.UserId);

        RaiseNetworkEvent(new FSPerksStateEvent
        {
            PerkPoints = ap,
            Levels = new Dictionary<string, int>(aug.Levels),
            Slots = (string[])aug.Slots.Clone(),
            Loadouts = aug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(session));
    }

    private const int MaxJsonBytes = 65_536; // 64 KB — any legitimate augment payload is <1 KB

    private void DeserializeInto(FSPerkLevelsComponent aug,
        string levelsJson, string slotsJson, string loadoutsJson)
    {
        if (levelsJson.Length > MaxJsonBytes || slotsJson.Length > MaxJsonBytes || loadoutsJson.Length > MaxJsonBytes)
        {
            Log.Error($"[FSPerk] Oversized JSON in augment data (levels={levelsJson.Length} slots={slotsJson.Length} loadouts={loadoutsJson.Length}) — discarding all.");
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
                    aug.Levels = levels;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSPerk] Failed to deserialize augment levels JSON: {e.Message}\nJSON was: {levelsJson}");
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
                            (!FSPerkDef.All.ContainsKey(slots[i]) || aug.GetLevel(slots[i]) <= 0))
                        {
                            Log.Warning($"[FSPerk] Slot {i} contained invalid/unowned augment '{slots[i]}' — clearing.");
                            slots[i] = string.Empty;
                        }
                    }
                    aug.Slots = slots;
                }
                else if (slots != null)
                {
                    Log.Warning($"[FSPerk] Slot JSON had {slots.Length} entries but SlotCount is {FSPerkDef.SlotCount} — discarding slots (SlotCount may have changed).");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSPerk] Failed to deserialize augment slots JSON: {e.Message}\nJSON was: {slotsJson}");
            }
        }

        if (!string.IsNullOrEmpty(loadoutsJson))
        {
            try
            {
                var loadouts = JsonSerializer.Deserialize<string[][]>(loadoutsJson);
                if (loadouts?.Length == 3)
                {
                    for (var i = 0; i < 3; i++)
                    {
                        if (loadouts[i]?.Length == FSPerkDef.SlotCount)
                            aug.Loadouts[i] = loadouts[i];
                        else if (loadouts[i] != null)
                            Log.Warning($"[FSPerk] Loadout {i} had {loadouts[i]!.Length} entries — discarding (SlotCount mismatch).");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSPerk] Failed to deserialize augment loadouts JSON: {e.Message}\nJSON was: {loadoutsJson}");
            }
        }
    }
}
