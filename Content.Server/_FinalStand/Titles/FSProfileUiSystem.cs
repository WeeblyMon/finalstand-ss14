using System.Linq;
using Content.Server._FinalStand.Mentor;
using Content.Shared._FinalStand.Mentor;
using Content.Shared._FinalStand.Titles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Titles;

public sealed partial class FSProfileUiSystem : EntitySystem
{
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private FSMentorSystem _mentor = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan RequestInterval = TimeSpan.FromSeconds(0.25);

    private readonly Dictionary<NetUserId, TimeSpan> _lastRequest = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSTitlesRequestEvent>((_, args) => Throttled(args, SendTitles));
        SubscribeNetworkEvent<FSTitleEquipEvent>(OnEquip);
        SubscribeNetworkEvent<FSMentorRequestEvent>((_, args) => Throttled(args, s => SendMentor(s, null)));
        SubscribeNetworkEvent<FSMentorBuyEvent>(OnBuy);
    }

    private void Throttled(EntitySessionEventArgs args, Action<ICommonSession> action)
    {
        var user = args.SenderSession.UserId;
        var now = _timing.RealTime;
        if (_lastRequest.TryGetValue(user, out var last) && now - last < RequestInterval)
            return;

        _lastRequest[user] = now;
        action(args.SenderSession);
    }

    public void SendTitles(ICommonSession session)
    {
        RaiseNetworkEvent(new FSTitlesStateEvent
        {
            Owned = _titles.GetOwned(session.UserId).ToList(),
            Equipped = _titles.GetEquipped(session.UserId),
        }, session);
    }

    public void SendMentor(ICommonSession session, string? message)
    {
        var record = _mentor.CurrentRecord(session.UserId.UserId);
        var (playtime, level, _) = _mentor.MentorStanding(session);
        var owned = _titles.GetOwned(session.UserId).ToHashSet();

        RaiseNetworkEvent(new FSMentorStateEvent
        {
            Balance = record.Balance,
            Total = record.Total,
            WeekPoints = record.WeekPoints,
            WeekCap = FSMentorLedger.WeekCap,
            Eligible = _mentor.IsEligibleMentor(session),
            Hours = playtime.TotalHours,
            MinHours = FSMentorRules.MinPlaytime.TotalHours,
            Level = level,
            MinLevel = FSMentorRules.MinLevel,
            Shop = FSMentorSystem.Shop.Select(i => new FSMentorShopEntry(i.Id, i.Cost, i.Title, i.Title is { } t && owned.Contains(t), i.PerkPoints)).ToList(),
            Message = message,
        }, session);
    }

    private void OnEquip(FSTitleEquipEvent msg, EntitySessionEventArgs args)
    {
        Throttled(args, session =>
        {
            if (msg.Title is not { } title)
                _titles.Equip(session.UserId, null);
            else if (title.Id is not null && _proto.HasIndex(title))
                _titles.Equip(session.UserId, title);

            SendTitles(session);
        });
    }

    private void OnBuy(FSMentorBuyEvent msg, EntitySessionEventArgs args)
    {
        Throttled(args, session =>
        {
            _mentor.TryBuy(session.UserId, msg.Item, out var message);
            SendMentor(session, message);
            SendTitles(session);
        });
    }
}
