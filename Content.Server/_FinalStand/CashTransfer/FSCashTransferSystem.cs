using Content.Server._FinalStand.Economy;
using Content.Server.Popups;
using Content.Shared._FinalStand.CashTransfer;
using Content.Shared._FinalStand.Economy;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.CashTransfer;

public sealed class FSCashTransferSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    private static readonly EntProtoId SessionProto = "FSCashTransferSession";

    public override void Initialize()
    {
        base.Initialize();

        // InnateVerb fires at the USER entity, so uid = right-clicker who has MindContainerComponent
        SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<InnateVerb>>(OnGetVerbs);

        // BUI events are on the session entity (which has FSCashTransferComponent)
        Subs.BuiEvents<FSCashTransferComponent>(FSCashTransferUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<FSCashTransferRequestMessage>(OnTransferRequest);
        });
    }

    private void OnGetVerbs(EntityUid uid, MindContainerComponent mindComp, GetVerbsEvent<InnateVerb> args)
    {
        // uid = args.User = the right-clicker; args.Target = the entity being right-clicked
        var target = args.Target;

        if (uid == target)
            return;

        if (!args.CanAccess || !args.CanInteract)
            return;

        if (_mobState.IsDead(uid))
            return;

        // Target must be a player mob with a mind
        if (!_mind.TryGetMind(target, out var targetMindId, out _))
            return;
        if (!HasComp<FSPlayerWalletComponent>(targetMindId))
            return;

        // Sender must have a wallet
        if (!_mind.TryGetMind(uid, out var senderMindId, out _))
            return;
        if (!HasComp<FSPlayerWalletComponent>(senderMindId))
            return;

        args.Verbs.Add(new InnateVerb
        {
            Text = Loc.GetString("fs-cash-transfer-verb"),
            Impact = LogImpact.Medium,
            Act = () => OpenTransferDialog(uid, target),
        });
    }

    private void OpenTransferDialog(EntityUid sender, EntityUid target)
    {
        var session = Spawn(SessionProto, Transform(sender).Coordinates);
        var sessionComp = EnsureComp<FSCashTransferSessionComponent>(session);
        sessionComp.Target = target;

        _uiSystem.OpenUi(session, FSCashTransferUiKey.Key, sender);
    }

    private void OnUiOpened(EntityUid uid, FSCashTransferComponent comp, BoundUIOpenedEvent args)
    {
        if (!TryComp<FSCashTransferSessionComponent>(uid, out var session))
            return;

        // Re-validate range at the moment the server processes the open event
        if (!_interaction.InRangeUnobstructed(args.Actor, session.Target))
        {
            _uiSystem.CloseUi(uid, FSCashTransferUiKey.Key, args.Actor);
            return;
        }

        SendState(uid, args.Actor, session.Target);
    }

    private void OnUiClosed(EntityUid uid, FSCashTransferComponent comp, BoundUIClosedEvent args)
    {
        // Delete the session entity when the dialog is closed for any reason
        if (!TerminatingOrDeleted(uid))
            Del(uid);
    }

    private void SendState(EntityUid sessionUid, EntityUid senderUid, EntityUid targetUid)
    {
        var balance = 0;
        if (_mind.TryGetMind(senderUid, out var mindId, out _) &&
            TryComp<FSPlayerWalletComponent>(mindId, out var wallet))
        {
            balance = wallet.Credits;
        }

        var state = new FSCashTransferBuiState(MetaData(targetUid).EntityName, balance);
        _uiSystem.SetUiState(sessionUid, FSCashTransferUiKey.Key, state);
    }

    private void OnTransferRequest(EntityUid uid, FSCashTransferComponent comp, FSCashTransferRequestMessage args)
    {
        if (!TryComp<FSCashTransferSessionComponent>(uid, out var session))
            return;

        var sender = args.Actor;
        var target = session.Target;

        // Guards
        if (sender == target)
            return;
        if (args.Amount <= 0)
            return;
        if (!_interaction.InRangeUnobstructed(sender, target))
        {
            _popup.PopupEntity(Loc.GetString("fs-cash-transfer-fail-range"), sender, sender, PopupType.SmallCaution);
            return;
        }
        if (_mobState.IsDead(sender) || _mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("fs-cash-transfer-fail-dead"), sender, sender, PopupType.SmallCaution);
            return;
        }
        if (!_mind.TryGetMind(sender, out var senderMindId, out _) ||
            !TryComp<FSPlayerWalletComponent>(senderMindId, out _))
        {
            return;
        }
        if (!_mind.TryGetMind(target, out var targetMindId, out _) ||
            !TryComp<FSPlayerWalletComponent>(targetMindId, out _))
        {
            _popup.PopupEntity(Loc.GetString("fs-cash-transfer-fail-dead"), sender, sender, PopupType.SmallCaution);
            return;
        }

        // ATOMIC: deduct from sender first — only credit target on success
        if (!_wallet.TryDeductCredits(senderMindId, args.Amount))
        {
            _popup.PopupEntity(Loc.GetString("fs-cash-transfer-fail-funds"), sender, sender, PopupType.SmallCaution);
            return;
        }

        _wallet.GiveCredits(targetMindId, args.Amount);

        _adminLogger.Add(
            LogType.StorePurchase,
            LogImpact.Medium,
            $"{ToPrettyString(sender):sender} transferred {args.Amount} credits to {ToPrettyString(target):recipient}");

        _popup.PopupEntity(
            Loc.GetString("fs-cash-transfer-sent", ("amount", args.Amount), ("name", MetaData(target).EntityName)),
            sender, sender, PopupType.Large);

        _popup.PopupEntity(
            Loc.GetString("fs-cash-transfer-received", ("amount", args.Amount), ("name", MetaData(sender).EntityName)),
            target, target, PopupType.Large);

        _uiSystem.CloseUi(uid, FSCashTransferUiKey.Key, sender);
    }
}
