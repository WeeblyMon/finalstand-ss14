// The pilot's HUD and weapon keys: while you're in a mech, 1/2/3 jump to that mounted weapon instead of the action bar,
// and a combat button under the weapon cards stands in for the combat toggle those keys cover.
using Content.Client.Actions;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Input;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.Mech;

public sealed class FSMechHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;

    public const int WeaponKeys = 3;

    private FSMechHudOverlay _overlay = default!;
    private FSMechCombatButton? _button;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSMechHudOverlay();
        _overlays.AddOverlay(_overlay);

        var keys = new[] { ContentKeyFunctions.Hotbar1, ContentKeyFunctions.Hotbar2, ContentKeyFunctions.Hotbar3 };
        var builder = CommandBinds.Builder;
        for (var i = 0; i < WeaponKeys; i++)
            builder = builder.BindBefore(keys[i], new PilotOnlyHandler(this, i), typeof(ActionUIController));
        builder.Register<FSMechHudSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSMechHudOverlay>();
        CommandBinds.Unregister<FSMechHudSystem>();
        _button?.Orphan();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (Piloted() == null || _overlay.CombatBox is not { } box || _ui.ActiveScreen is not { } screen)
        {
            if (_button != null)
                _button.Visible = false;
            return;
        }

        if (_button == null || _button.Parent != screen)
        {
            _button?.Orphan();
            _button = new FSMechCombatButton();
            _button.OnToggle += ToggleCombat;
            screen.AddChild(_button);
        }

        var scale = _button.UIScale;
        _button.Visible = true;
        _button.InCombat = _player.LocalEntity is { } local && _combat.IsInCombatMode(local);
        _button.SetSize = box.Size / scale;
        LayoutContainer.SetPosition(_button, box.TopLeft / scale);
    }

    private void ToggleCombat()
    {
        if (_player.LocalEntity is not { } local
            || !TryComp<CombatModeComponent>(local, out var combat)
            || combat.CombatToggleActionEntity is not { } action
            || !TryComp<ActionComponent>(action, out var actionComp))
            return;

        _actions.TriggerAction((action, actionComp));
    }

    public Entity<MechComponent>? Piloted()
    {
        if (_player.LocalEntity is not { } local
            || !TryComp<VehicleOperatorComponent>(local, out var op)
            || op.Vehicle is not { } vehicle
            || !TryComp<MechComponent>(vehicle, out var mech))
            return null;

        return (vehicle, mech);
    }

    private sealed class PilotOnlyHandler(FSMechHudSystem system, int index) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            if (system.Piloted() == null)
                return false;

            if (message.State == BoundKeyState.Down)
                system.RaiseNetworkEvent(new FSMechSelectWeaponEvent(index));

            return true;
        }
    }
}
