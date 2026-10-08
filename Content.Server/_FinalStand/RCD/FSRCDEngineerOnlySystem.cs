using Content.Server._FinalStand.Departments;
using Content.Server.Destructible;
using Content.Server.Stack;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Hands.Components;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem : EntitySystem
{
    [Dependency] private FSDepartmentAccessSystem _engineering = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan UsageRefreshInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan ReservationTimeout = TimeSpan.FromSeconds(30);
    private readonly Dictionary<EntityUid, TimeSpan> _reservedAt = new();
    private readonly List<EntityUid> _expired = new();
    private TimeSpan _nextUsageRefresh;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(RCDSystem)]);
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, RCDDoAfterEvent>(OnDoAfter,
            after: [typeof(RCDSystem)]);
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, FSRCDBuiltEvent>(OnBuilt);
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, FSRCDDelayEvent>(OnDelay);
        SubscribeNetworkEvent<FSRCDProbeRequestEvent>(OnProbe);
        SubscribeNetworkEvent<FSRCDBeltPickEvent>(OnBeltPick);
    }

    private void OnDoAfter(Entity<FSEngineerOnlyRCDComponent> ent, ref RCDDoAfterEvent args)
    {
        if (args.Cancelled)
            Release(ent);

        if (!args.Cancelled && args.Args.Target is { } target && EntityManager.IsQueuedForDeletion(target))
        {
            RefundDeconstruct(target);
            RefreshUsage(ent, args.User);
        }
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUsageRefresh)
            return;

        _nextUsageRefresh = _timing.CurTime + UsageRefreshInterval;

        // A build that never started (vanilla refused it after us) would otherwise hold its silo stock forever.
        foreach (var (rcd, at) in _reservedAt)
        {
            if (_timing.CurTime - at > ReservationTimeout)
                _expired.Add(rcd);
        }

        foreach (var rcd in _expired)
        {
            Release(rcd);
            _reservedAt.Remove(rcd);
        }

        _expired.Clear();
        int? station = null;
        var query = EntityQueryEnumerator<FSEngineerOnlyRCDComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (HasComp<HandsComponent>(xform.ParentUid))
                ApplyUsage((uid, comp), xform.ParentUid, station ??= CountBuilt());
        }
    }

    private void OnAfterInteract(EntityUid uid, FSEngineerOnlyRCDComponent comp, AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target is { } target && TryComp<FSHMGComponent>(target, out var hmg) && hmg.Gunner != null)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("fs-rcd-hmg-crewed"), uid, args.User, PopupType.Medium);
            return;
        }

        if (args.Target is { } owned && IsDeconstructing(uid) && TakeDownBlock(args.User, owned) is { } refusal)
        {
            args.Handled = true;
            _popup.PopupEntity(refusal, uid, args.User, PopupType.Medium);
            return;
        }

        if (GetBlockReason(uid, comp, args.User, args.ClickLocation) is { } reason)
        {
            args.Handled = true;
            _popup.PopupEntity(reason, uid, args.User, PopupType.Medium);
            return;
        }

        if (RecipeFor(uid) is { } recipe && !TryReserve(uid, args.User, recipe))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("fs-rcd-silo-busy"), uid, args.User, PopupType.Medium);
            return;
        }

        _reservedAt[uid] = _timing.CurTime;
    }

    private bool IsDeconstructing(EntityUid rcd)
    {
        return TryComp<RCDComponent>(rcd, out var rcdComp) && _proto.Index(rcdComp.ProtoId).Mode == RcdMode.Deconstruct;
    }

    private FSBuildRecipePrototype? RecipeFor(EntityUid rcd)
    {
        return TryComp<RCDComponent>(rcd, out var rcdComp)
               && _proto.Index(rcdComp.ProtoId).Mode == RcdMode.ConstructObject
               && _proto.TryIndex<FSBuildRecipePrototype>(rcdComp.ProtoId.Id, out var recipe)
            ? recipe
            : null;
    }
}
