using Content.Server._FinalStand.Science;
using Content.Server.Popups;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Placement;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._FinalStand.Deployables;

// Domain logic (stock, Science-only gating) for the Null Field and Damage Beacon deployables - placement itself lives in FSPlacementSystem.
public sealed partial class FSDeployableSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FSScienceOnlySystem _science = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDeployableItemComponent, FSPlacementConfirmedEvent>(OnPlacementConfirmed);
        SubscribeLocalEvent<FSDeployableItemComponent, LandEvent>(OnLand);
    }

    private void OnPlacementConfirmed(EntityUid uid, FSDeployableItemComponent comp, FSPlacementConfirmedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryDeploy(uid, comp, args.Coordinates, args.User);
    }

    // Thrown item deploys at its landing spot, not the thrower's position; the physical item stays on the ground with Stock decremented.
    private void OnLand(EntityUid uid, FSDeployableItemComponent comp, ref LandEvent args)
    {
        var coords = Transform(uid).Coordinates;
        if (!TryDeploy(uid, comp, coords, args.User))
            return;

        // Nudge the leftover item off the anchored structure's tile - otherwise it's unclickable (the structure wins click resolution).
        _transform.SetCoordinates(uid, coords.Offset(new Vector2(0.35f, 0.35f)));
    }

    private bool TryDeploy(EntityUid uid, FSDeployableItemComponent comp, EntityCoordinates coords, EntityUid? user)
    {
        // Backstop for the throw path, which bypasses FSScienceOnlySystem's UseInHandEvent gate.
        if (user != null && !_science.IsScience(user.Value))
        {
            _popup.PopupEntity(Loc.GetString("fs-science-only-use"), user.Value, user.Value);
            return false;
        }

        if (comp.Stock <= 0)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("fs-deployable-no-stock"), user.Value, user.Value);
            return false;
        }

        var deployed = Spawn(comp.DeployedProtoId, coords);
        _transform.AnchorEntity(deployed);

        comp.Stock--;
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("fs-deployable-placed"), deployed, user ?? deployed);
        return true;
    }
}
