using Content.Server._FinalStand.Science;
using Content.Server.Popups;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Placement;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._FinalStand.Deployables;

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

    private void OnLand(EntityUid uid, FSDeployableItemComponent comp, ref LandEvent args)
    {
        var coords = Transform(uid).Coordinates;
        if (!TryDeploy(uid, comp, coords, args.User))
            return;

        _transform.SetCoordinates(uid, coords.Offset(new Vector2(0.35f, 0.35f)));
    }

    private bool TryDeploy(EntityUid uid, FSDeployableItemComponent comp, EntityCoordinates coords, EntityUid? user)
    {
        if (user is not { } deployer)
            return false;

        if (!_science.IsScience(deployer))
        {
            _popup.PopupEntity(Loc.GetString("fs-science-only-use"), deployer, deployer);
            return false;
        }

        if (comp.Stock <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-deployable-no-stock"), deployer, deployer);
            return false;
        }

        var deployed = Spawn(comp.DeployedProtoId, coords);

        if (!_transform.AnchorEntity(deployed))
        {
            Del(deployed);
            _popup.PopupEntity(Loc.GetString("fs-deployable-no-anchor"), deployer, deployer);
            return false;
        }

        comp.Stock--;
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("fs-deployable-placed"), deployed, deployer);
        return true;
    }
}
