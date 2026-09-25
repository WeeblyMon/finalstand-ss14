using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Science;
using Content.Server.Popups;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Placement;
using Content.Shared.Mind;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server._FinalStand.Deployables;

public sealed partial class FSDeployableSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FSScienceOnlySystem _science = default!;
    [Dependency] private MedicalOps.FSMedicalRosterSystem _roster = default!;
    [Dependency] private FSDepartmentAccessSystem _engineering = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private Perks.FSTechnicianSystem _technician = default!;

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

        args.Handled = TryDeploy(uid, comp, args.Coordinates, args.User, args.Direction);
    }

    private void OnLand(EntityUid uid, FSDeployableItemComponent comp, ref LandEvent args)
    {
        var coords = Transform(uid).Coordinates;
        if (!TryDeploy(uid, comp, coords, args.User))
            return;

        _transform.SetCoordinates(uid, coords.Offset(new Vector2(0.35f, 0.35f)));
    }

    private bool TryDeploy(EntityUid uid, FSDeployableItemComponent comp, EntityCoordinates coords, EntityUid? user,
        Direction? facing = null)
    {
        if (user is not { } deployer)
            return false;

        if (comp.RequiresScience && !_science.IsScience(deployer))
        {
            _popup.PopupEntity(Loc.GetString("fs-science-only-use"), deployer, deployer);
            return false;
        }

        if (comp.RequiresMedical && !_roster.IsMedical(deployer))
        {
            _popup.PopupEntity(Loc.GetString("fs-medical-only"), deployer, deployer);
            return false;
        }

        if (comp.RequiresEngineering && !_engineering.IsEngineering(deployer))
        {
            _popup.PopupEntity(Loc.GetString("fs-engineering-only-use"), deployer, deployer);
            return false;
        }

        if (comp.Stock <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-deployable-no-stock"), deployer, deployer);
            return false;
        }

        var ownerMind = _mind.TryGetMind(deployer, out var mindId, out _) ? mindId : (EntityUid?) null;

        var maxDeployed = comp.MaxDeployed > 0 ? comp.MaxDeployed + _technician.GetBonusForUser(deployer, comp) : 0;
        if (maxDeployed > 0 && CountDeployed(ownerMind, comp.DeployedProtoId) >= maxDeployed)
        {
            _popup.PopupEntity(Loc.GetString("fs-deployable-max-deployed", ("max", maxDeployed)), deployer, deployer);
            return false;
        }

        var deployed = Spawn(comp.DeployedProtoId, coords);

        if (comp.FaceDeployerDirection)
            _transform.SetLocalRotation(deployed, (facing ?? Transform(deployer).LocalRotation.GetCardinalDir()).ToAngle());

        if (!_transform.AnchorEntity(deployed))
        {
            Del(deployed);
            _popup.PopupEntity(Loc.GetString("fs-deployable-no-anchor"), deployer, deployer);
            return false;
        }

        var deployedBy = EnsureComp<FSDeployedByComponent>(deployed);
        deployedBy.OwnerMind = ownerMind;
        deployedBy.DeployedBy = deployer;
        deployedBy.SourceProto = comp.DeployedProtoId;
        deployedBy.SourceItem = uid;

        var ev = new FSDeployableDeployedEvent(uid, deployer);
        RaiseLocalEvent(deployed, ref ev);

        comp.Stock--;
        Dirty(uid, comp);

        _popup.PopupEntity(Loc.GetString("fs-deployable-placed"), deployed, deployer);
        return true;
    }

    public int CountDeployed(EntityUid? ownerMind, EntProtoId proto)
    {
        var count = 0;
        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (deployedBy.OwnerMind == ownerMind && deployedBy.SourceProto == proto && !TerminatingOrDeleted(uid))
                count++;
        }

        return count;
    }

    public void ClearDeployed(EntityUid? ownerMind, EntProtoId proto)
    {
        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (deployedBy.OwnerMind == ownerMind && deployedBy.SourceProto == proto && !TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
    }
}
