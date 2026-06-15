using System.Linq;
using System.Text.Json;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Augments;

public sealed class FSAugmentSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<ICommonSession, TimeSpan> _stateRequestCooldown = new();
    private static readonly TimeSpan StateRequestInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<FSAugmentStateRequestMessage>(OnStateRequested);
        SubscribeNetworkEvent<FSBuyAugmentMessage>(OnBuyAugment);
        SubscribeNetworkEvent<FSEquipAugmentMessage>(OnEquipAugment);
        SubscribeNetworkEvent<FSUnequipAugmentMessage>(OnUnequipAugment);
        SubscribeNetworkEvent<FSSaveLoadoutMessage>(OnSaveLoadout);
        SubscribeNetworkEvent<FSLoadLoadoutMessage>(OnLoadLoadout);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind))
        {
            Log.Debug($"[FSAugment] OnPlayerAttached: TryGetMind failed for entity={ev.Entity} — skipping ECS component setup");
            return;
        }
        if (mind.UserId == null)
        {
            Log.Debug($"[FSAugment] OnPlayerAttached: mind={mindId} has no UserId — skipping");
            return;
        }

        var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
        var aug = EnsureComp<FSAugmentLevelsComponent>(mindId);
        DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);
        Log.Debug($"[FSAugment] OnPlayerAttached: added FSAugmentLevelsComponent to mind={mindId} for entity={ev.Entity}");

        SendStateToClient(mindId, aug, mind.UserId.Value.UserId);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind)) return;

        if (mind?.UserId != null &&
            _playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            _stateRequestCooldown.Remove(session);

        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug)) return;
        SaveToDb(mindId, aug);
    }

    private void OnStateRequested(FSAugmentStateRequestMessage msg, EntitySessionEventArgs args)
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
        var tempAug = new FSAugmentLevelsComponent();
        DeserializeInto(tempAug, levelsJson, slotsJson, loadoutsJson);
        var ap = _wallet.GetStoredAugmentPoints(userId);

        RaiseNetworkEvent(new FSAugmentsStateEvent
        {
            AugmentPoints = ap,
            Levels = new Dictionary<string, int>(tempAug.Levels),
            Slots = (string[])tempAug.Slots.Clone(),
            Loadouts = tempAug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(args.SenderSession));
    }

    private void OnBuyAugment(FSBuyAugmentMessage msg, EntitySessionEventArgs args)
    {
        if (!FSAugmentDef.All.ContainsKey(msg.AugmentId))
        {
            Log.Debug($"[FSAugment] OnBuyAugment: unknown augment id '{msg.AugmentId}'");
            return;
        }

        if (_mind.TryGetMind(args.SenderSession, out var mindId, out var mind))
        {
            OnBuyAugmentInRound(msg, args.SenderSession, mindId, mind);
        }
        else
        {
            OnBuyAugmentLobby(msg, args.SenderSession);
        }
    }

    private void OnBuyAugmentInRound(FSBuyAugmentMessage msg, ICommonSession session,
        EntityUid mindId, MindComponent? mind)
    {
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug))
        {
            if (mind?.UserId == null)
            {
                Log.Debug($"[FSAugment] OnBuyAugment: no UserId on mind {mindId}");
                return;
            }
            var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
            aug = EnsureComp<FSAugmentLevelsComponent>(mindId);
            DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);
        }

        var currentLevel = aug.GetLevel(msg.AugmentId);
        if (currentLevel >= FSAugmentDef.MaxLevel)
        {
            Log.Debug($"[FSAugment] OnBuyAugment: '{msg.AugmentId}' already max level ({currentLevel})");
            return;
        }

        var cost = FSAugmentDef.CostForUpgrade(currentLevel);
        if (!_wallet.TryDeductAugmentPoints(mindId, cost))
        {
            Log.Debug($"[FSAugment] OnBuyAugment: TryDeductAugmentPoints failed — mind={mindId} cost={cost}");
            return;
        }

        aug.Levels[msg.AugmentId] = currentLevel + 1;
        Log.Debug($"[FSAugment] OnBuyAugment: SUCCESS — '{msg.AugmentId}' → Lv{currentLevel + 1} for {session.Name}");
        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
    }

    private void OnBuyAugmentLobby(FSBuyAugmentMessage msg, ICommonSession session)
    {
        var userId = session.UserId.UserId;
        var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(userId);
        var aug = new FSAugmentLevelsComponent();
        DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);

        var currentLevel = aug.GetLevel(msg.AugmentId);
        if (currentLevel >= FSAugmentDef.MaxLevel)
        {
            Log.Debug($"[FSAugment] OnBuyAugment (lobby): '{msg.AugmentId}' already max level");
            return;
        }

        var cost = FSAugmentDef.CostForUpgrade(currentLevel);
        var currentAp = _wallet.GetStoredAugmentPoints(userId);
        if (currentAp < cost)
        {
            Log.Debug($"[FSAugment] OnBuyAugment (lobby): insufficient AP — have {currentAp}, need {cost}");
            return;
        }

        aug.Levels[msg.AugmentId] = currentLevel + 1;
        var newAp = currentAp - cost;

        _wallet.GiveAugmentPoints(session, -cost);
        _wallet.SaveAugmentDataByUser(userId,
            JsonSerializer.Serialize(aug.Levels),
            JsonSerializer.Serialize(aug.Slots),
            JsonSerializer.Serialize(aug.Loadouts));

        Log.Debug($"[FSAugment] OnBuyAugment (lobby): SUCCESS — '{msg.AugmentId}' → Lv{currentLevel + 1} for {session.Name}");

        if (!_playerManager.TryGetSessionById(session.UserId, out var pSession))
            return;

        RaiseNetworkEvent(new WalletUpdatedEvent(0, newAp), Filter.SinglePlayer(pSession));
        RaiseNetworkEvent(new FSAugmentsStateEvent
        {
            AugmentPoints = newAp,
            Levels = new Dictionary<string, int>(aug.Levels),
            Slots = (string[])aug.Slots.Clone(),
            Loadouts = aug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(pSession));
    }

    private void OnEquipAugment(FSEquipAugmentMessage msg, EntitySessionEventArgs args)
    {
        if (!FSAugmentDef.All.ContainsKey(msg.AugmentId)) return;
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSAugmentDef.SlotCount) return;

        DispatchAugMutation(args.SenderSession, aug =>
        {
            if (aug.GetLevel(msg.AugmentId) <= 0) return false;
            if (!string.IsNullOrEmpty(aug.Slots[msg.SlotIndex])) return false;
            if (aug.Slots.Contains(msg.AugmentId)) return false;
            aug.Slots[msg.SlotIndex] = msg.AugmentId;
            return true;
        });
    }

    private void OnUnequipAugment(FSUnequipAugmentMessage msg, EntitySessionEventArgs args)
    {
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSAugmentDef.SlotCount) return;

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
            Array.Copy(aug.Slots, aug.Loadouts[msg.LoadoutIndex], FSAugmentDef.SlotCount);
            return true;
        });
    }

    private void OnLoadLoadout(FSLoadLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        DispatchAugMutation(args.SenderSession, aug =>
        {
            var src = aug.Loadouts[msg.LoadoutIndex];
            for (var i = 0; i < FSAugmentDef.SlotCount; i++)
            {
                var id = src[i];
                aug.Slots[i] = !string.IsNullOrEmpty(id) && aug.GetLevel(id) > 0 ? id : string.Empty;
            }
            return true;
        });
    }

    // Runs a mutation on a player's FSAugmentLevelsComponent, saving and notifying on success.
    // Handles both in-round (mind entity) and lobby (DB-direct) cases transparently.
    private void DispatchAugMutation(ICommonSession session, Func<FSAugmentLevelsComponent, bool> mutate)
    {
        if (_mind.TryGetMind(session, out var mindId, out var mind))
        {
            // In-round path: operate on the ECS component, creating it lazily if OnPlayerAttached missed it.
            var aug = EnsureAugComponent(mindId, mind);
            if (aug == null) return;
            if (!mutate(aug)) return;
            SaveToDb(mindId, aug);
            SendStateToClient(mindId, aug);
            return;
        }

        // Lobby path: no mind yet — operate on DB directly
        var userId = session.UserId.UserId;
        var (lj, sj, oj) = _wallet.LoadAugmentData(userId);
        var lobbyAug = new FSAugmentLevelsComponent();
        DeserializeInto(lobbyAug, lj, sj, oj);

        if (!mutate(lobbyAug)) return;

        _wallet.SaveAugmentDataByUser(userId,
            JsonSerializer.Serialize(lobbyAug.Levels),
            JsonSerializer.Serialize(lobbyAug.Slots),
            JsonSerializer.Serialize(lobbyAug.Loadouts));

        if (!_playerManager.TryGetSessionById(session.UserId, out var pSession)) return;

        var ap = _wallet.GetStoredAugmentPoints(userId);
        RaiseNetworkEvent(new FSAugmentsStateEvent
        {
            AugmentPoints = ap,
            Levels = new Dictionary<string, int>(lobbyAug.Levels),
            Slots = (string[])lobbyAug.Slots.Clone(),
            Loadouts = lobbyAug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(pSession));
    }

    // Returns the existing FSAugmentLevelsComponent on mindId, or creates one from DB if missing.
    // Returns null if the mind has no UserId (can't load data).
    private FSAugmentLevelsComponent? EnsureAugComponent(EntityUid mindId, MindComponent? mind)
    {
        if (TryComp<FSAugmentLevelsComponent>(mindId, out var existing))
            return existing;

        if (mind?.UserId == null)
            return null;

        var (lj, sj, oj) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
        var aug = EnsureComp<FSAugmentLevelsComponent>(mindId);
        DeserializeInto(aug, lj, sj, oj);
        Log.Debug($"[FSAugment] EnsureAugComponent: lazily created FSAugmentLevelsComponent on mind={mindId}");
        return aug;
    }

    private void SaveToDb(EntityUid mindId, FSAugmentLevelsComponent aug)
    {
        _wallet.SaveAugmentData(mindId,
            JsonSerializer.Serialize(aug.Levels),
            JsonSerializer.Serialize(aug.Slots),
            JsonSerializer.Serialize(aug.Loadouts));
    }

    private void SendStateToClient(EntityUid mindId, FSAugmentLevelsComponent aug, Guid? userId = null)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session)) return;

        int ap;
        if (TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
            ap = wallet.AugmentPoints;
        else
            ap = _wallet.GetStoredAugmentPoints(mind.UserId.Value.UserId);

        RaiseNetworkEvent(new FSAugmentsStateEvent
        {
            AugmentPoints = ap,
            Levels = new Dictionary<string, int>(aug.Levels),
            Slots = (string[])aug.Slots.Clone(),
            Loadouts = aug.Loadouts.Select(l => (string[])l.Clone()).ToArray(),
        }, Filter.SinglePlayer(session));
    }

    private void DeserializeInto(FSAugmentLevelsComponent aug,
        string levelsJson, string slotsJson, string loadoutsJson)
    {
        if (!string.IsNullOrEmpty(levelsJson))
        {
            try
            {
                var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(levelsJson);
                if (levels != null)
                {
                    foreach (var key in levels.Keys.Where(k => !FSAugmentDef.All.ContainsKey(k)).ToList())
                        levels.Remove(key);
                    aug.Levels = levels;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSAugment] Failed to deserialize augment levels JSON: {e.Message}\nJSON was: {levelsJson}");
            }
        }

        if (!string.IsNullOrEmpty(slotsJson))
        {
            try
            {
                var slots = JsonSerializer.Deserialize<string[]>(slotsJson);
                if (slots?.Length == FSAugmentDef.SlotCount)
                {
                    for (var i = 0; i < FSAugmentDef.SlotCount; i++)
                    {
                        if (!string.IsNullOrEmpty(slots[i]) &&
                            (!FSAugmentDef.All.ContainsKey(slots[i]) || aug.GetLevel(slots[i]) <= 0))
                        {
                            Log.Warning($"[FSAugment] Slot {i} contained invalid/unowned augment '{slots[i]}' — clearing.");
                            slots[i] = string.Empty;
                        }
                    }
                    aug.Slots = slots;
                }
                else if (slots != null)
                {
                    Log.Warning($"[FSAugment] Slot JSON had {slots.Length} entries but SlotCount is {FSAugmentDef.SlotCount} — discarding slots (SlotCount may have changed).");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSAugment] Failed to deserialize augment slots JSON: {e.Message}\nJSON was: {slotsJson}");
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
                        if (loadouts[i]?.Length == FSAugmentDef.SlotCount)
                            aug.Loadouts[i] = loadouts[i];
                        else if (loadouts[i] != null)
                            Log.Warning($"[FSAugment] Loadout {i} had {loadouts[i]!.Length} entries — discarding (SlotCount mismatch).");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[FSAugment] Failed to deserialize augment loadouts JSON: {e.Message}\nJSON was: {loadoutsJson}");
            }
        }
    }
}
