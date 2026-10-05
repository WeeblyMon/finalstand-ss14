// Plants and unplants a mech in siege mode. Planted, it is anchored, so the breach AI treats it as part of the line.
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Network;

namespace Content.Shared._FinalStand.Mech;

public sealed partial class FSSiegeModeSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSSiegeModeComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<FSSiegeModeComponent, FSSiegeModeActionEvent>(OnAction);
        SubscribeLocalEvent<FSSiegeModeComponent, FSSiegeModeDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<FSSiegeModeComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<FSSiegeModeComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnOperatorSet(Entity<FSSiegeModeComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.NewOperator is not { } pilot)
            return;

        _actions.AddAction(pilot, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
        _actions.SetToggled(ent.Comp.ActionEntity, ent.Comp.Planted);
        Dirty(ent);
    }

    private void OnAction(Entity<FSSiegeModeComponent> ent, ref FSSiegeModeActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var time = ent.Comp.Planted ? ent.Comp.UnplantTime : ent.Comp.PlantTime;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, time, new FSSiegeModeDoAfterEvent(), ent, ent)
        {
            BreakOnDamage = false,
            NeedHand = false,
        });
    }

    private void OnDoAfter(Entity<FSSiegeModeComponent> ent, ref FSSiegeModeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        SetPlanted(ent, !ent.Comp.Planted);
        _popup.PopupPredicted(Loc.GetString(ent.Comp.Planted ? "fs-siege-planted" : "fs-siege-unplanted"), ent, args.User);
    }

    public void SetPlanted(Entity<FSSiegeModeComponent> ent, bool planted)
    {
        ent.Comp.Planted = planted;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ActionEntity, planted);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        _damageable.SetDamageModifierSetId(ent.Owner, planted ? ent.Comp.SiegeArmor : ent.Comp.NormalArmor);

        if (_net.IsClient)
            return;

        var xform = Transform(ent);
        if (planted && !xform.Anchored)
            _transform.AnchorEntity((ent.Owner, xform));
        else if (!planted && xform.Anchored)
            _transform.Unanchor(ent.Owner, xform);
    }

    private void OnRefreshSpeed(Entity<FSSiegeModeComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Planted)
            args.ModifySpeed(0f, 0f);
    }

    private void OnDamageModify(Entity<FSSiegeModeComponent> ent, ref DamageModifyEvent args)
    {
        if (!ent.Comp.Planted || args.Origin is not { } origin || !Exists(origin))
            return;

        var toAttacker = _transform.GetWorldPosition(origin) - _transform.GetWorldPosition(ent);
        if (toAttacker.LengthSquared() < 0.001f)
            return;

        var facing = _transform.GetWorldRotation(ent).ToWorldVec();
        var angle = Angle.FromWorldVec(toAttacker) - Angle.FromWorldVec(facing);
        if (Math.Abs(angle.Reduced().Degrees) <= ent.Comp.FrontalArc / 2f)
            args.Damage *= ent.Comp.FrontalDamage;
    }
}
