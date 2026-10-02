using Content.Server._FinalStand.Departments;
using Content.Server.Destructible;
using Content.Server.Stack;
using Content.Shared.Damage.Components;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Robust.Shared.Map.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem : EntitySystem
{
    [Dependency] private FSDepartmentAccessSystem _engineering = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private StackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(RCDSystem)]);
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, RCDDoAfterEvent>(OnDoAfter,
            after: [typeof(RCDSystem)]);
    }

    private void OnDoAfter(Entity<FSEngineerOnlyRCDComponent> ent, ref RCDDoAfterEvent args)
    {
        if (args.Cancelled
            || args.Args.Target is not { } target
            || !EntityManager.IsQueuedForDeletion(target)
            || !TryComp<FSDeconstructRefundComponent>(target, out var refund))
            return;

        var health = 1f;
        if (_destructible.TryGetDestroyedAt(target, out var destroyedAt) && destroyedAt > 0
            && TryComp<DamageableComponent>(target, out var damageable))
            health = Math.Clamp(1f - (damageable.TotalDamage / destroyedAt.Value).Float(), 0f, 1f);

        var coords = Transform(target).Coordinates;
        foreach (var (material, count) in refund.Materials)
        {
            var amount = (int) MathF.Floor(count * health * refund.Fraction);
            if (amount > 0)
                _stack.SpawnMultipleAtPosition(material, amount, coords);
        }
    }

    private void OnAfterInteract(EntityUid uid, FSEngineerOnlyRCDComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // No-build markers apply to everyone, engineers included.
        if (IsBuildBlockedHere(uid, args))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("fs-rcd-build-blocked"), uid, args.User, PopupType.Medium);
            return;
        }

        if (_engineering.IsEngineering(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity("Can only be used by Engineers", uid, args.User, PopupType.Medium);
    }

    // Objects (walls, airlocks, windows) are blocked on marked tiles; floor tiles stay legal.
    private bool IsBuildBlockedHere(EntityUid rcd, AfterInteractEvent args)
    {
        if (!TryComp<RCDComponent>(rcd, out var rcdComp)
            || _proto.Index(rcdComp.ProtoId).Mode != RcdMode.ConstructObject)
            return false;

        var location = args.ClickLocation;
        if (!location.IsValid(EntityManager))
            return false;

        var gridUid = _transform.GetGrid(location) ?? _transform.GetGrid(args.User);
        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return false;

        var tile = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, location);
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid.Value, mapGrid, tile);

        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<FSNoRCDBuildComponent>(ent.Value))
                return true;
        }

        return false;
    }
}
