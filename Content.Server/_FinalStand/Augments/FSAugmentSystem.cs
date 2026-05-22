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
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out var mind) || mind.UserId == null)
            return;

        var (levelsJson, slotsJson, loadoutsJson) = _wallet.LoadAugmentData(mind.UserId.Value.UserId);
        var aug = EnsureComp<FSAugmentLevelsComponent>(mindId);
        DeserializeInto(aug, levelsJson, slotsJson, loadoutsJson);

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

        if (_mind.TryGetMind(args.SenderSession, out var mindId, out MindComponent? _) &&
            TryComp<FSAugmentLevelsComponent>(mindId, out var aug))
        {
            SendStateToClient(mindId, aug);
            return;
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
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug)) return;
        if (!FSAugmentDef.All.ContainsKey(msg.AugmentId)) return;

        var currentLevel = aug.GetLevel(msg.AugmentId);
        if (currentLevel >= FSAugmentDef.MaxLevel) return;

        var cost = FSAugmentDef.CostForUpgrade(currentLevel);
        if (!_wallet.TryDeductAugmentPoints(mindId, cost)) return;

        aug.Levels[msg.AugmentId] = currentLevel + 1;
        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
    }

    private void OnEquipAugment(FSEquipAugmentMessage msg, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug)) return;
        if (!FSAugmentDef.All.ContainsKey(msg.AugmentId)) return;
        if (aug.GetLevel(msg.AugmentId) <= 0) return;
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSAugmentDef.SlotCount) return;
        if (!string.IsNullOrEmpty(aug.Slots[msg.SlotIndex])) return;
        if (aug.Slots.Contains(msg.AugmentId)) return;

        aug.Slots[msg.SlotIndex] = msg.AugmentId;
        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
    }

    private void OnUnequipAugment(FSUnequipAugmentMessage msg, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug)) return;
        if (msg.SlotIndex < 0 || msg.SlotIndex >= FSAugmentDef.SlotCount) return;

        aug.Slots[msg.SlotIndex] = string.Empty;
        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
    }

    private void OnSaveLoadout(FSSaveLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug)) return;
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        Array.Copy(aug.Slots, aug.Loadouts[msg.LoadoutIndex], FSAugmentDef.SlotCount);
        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
    }

    private void OnLoadLoadout(FSLoadLoadoutMessage msg, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var aug)) return;
        if (msg.LoadoutIndex < 0 || msg.LoadoutIndex >= 3) return;

        var src = aug.Loadouts[msg.LoadoutIndex];
        for (var i = 0; i < FSAugmentDef.SlotCount; i++)
        {
            var id = src[i];
            aug.Slots[i] = !string.IsNullOrEmpty(id) && aug.GetLevel(id) > 0
                ? id
                : string.Empty;
        }

        SaveToDb(mindId, aug);
        SendStateToClient(mindId, aug);
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
