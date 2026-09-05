using Content.Server._FinalStand.Engineering;
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
    [Dependency] private FSEngineeringOnlySystem _engineering = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(RCDSystem)]);
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
