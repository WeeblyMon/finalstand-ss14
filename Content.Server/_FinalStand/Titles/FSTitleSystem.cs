using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared._FinalStand.Titles;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Titles;

public sealed partial class FSTitleSystem : EntitySystem
{
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSPlayerBadgeSystem _badge = default!;
    [Dependency] private FSTitleBuffSystem _buffs = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        ApplyBadgeTitle(ev.Player.UserId, ev.Mob);
    }

    public IReadOnlyList<ProtoId<FSTitlePrototype>> GetOwned(NetUserId userId)
    {
        return _store.GetTitles(userId.UserId)
            .Where(id => _proto.HasIndex<FSTitlePrototype>(id))
            .Select(id => new ProtoId<FSTitlePrototype>(id))
            .ToList();
    }

    public ProtoId<FSTitlePrototype>? GetEquipped(NetUserId userId)
    {
        var equipped = _store.GetProfile(userId.UserId).EquippedTitle;
        if (equipped == null || !_proto.HasIndex<FSTitlePrototype>(equipped))
            return null;
        return equipped;
    }

    public bool Grant(NetUserId userId, ProtoId<FSTitlePrototype> titleId)
    {
        if (!_proto.TryIndex(titleId, out var title))
        {
            Log.Error($"Tried to grant unknown title {titleId} to {userId}");
            return false;
        }

        if (!_store.AddTitle(userId.UserId, titleId, DateTime.UtcNow))
            return false;

        var autoEquip = GetEquipped(userId) == null;
        if (autoEquip)
            Equip(userId, titleId);

        if (_playerManager.TryGetSessionById(userId, out var session))
        {
            _chat.DispatchServerMessage(session, Loc.GetString(autoEquip ? "fs-title-earned-equipped" : "fs-title-earned",
                ("title", Loc.GetString(title.Name)),
                ("description", Loc.GetString(title.Description))));
        }

        Log.Info($"{userId} earned {titleId}");
        var earned = new FSTitleEarnedEvent(userId, titleId);
        RaiseLocalEvent(ref earned);
        return true;
    }

    public bool Equip(NetUserId userId, ProtoId<FSTitlePrototype>? titleId)
    {
        if (titleId != null && !GetOwned(userId).Contains(titleId.Value))
            return false;

        var profile = _store.GetProfile(userId.UserId);
        _store.SaveProfile(userId.UserId, profile with { EquippedTitle = titleId?.Id });

        if (_playerManager.TryGetSessionById(userId, out var session) && session.AttachedEntity is { } body)
            ApplyBadgeTitle(userId, body);

        return true;
    }

    public bool TryResolve(NetUserId userId, string query, out ProtoId<FSTitlePrototype> titleId)
    {
        foreach (var owned in GetOwned(userId))
        {
            var proto = _proto.Index(owned);
            if (string.Equals(owned.Id, query, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Loc.GetString(proto.Name), query, StringComparison.OrdinalIgnoreCase))
            {
                titleId = owned;
                return true;
            }
        }

        titleId = default;
        return false;
    }

    private void ApplyBadgeTitle(NetUserId userId, EntityUid body)
    {
        var badge = EnsureComp<FSPlayerBadgeComponent>(body);
        _badge.SetBadge(body, badge.Rookie, GetEquipped(userId));
        _buffs.Apply(body);
    }
}
