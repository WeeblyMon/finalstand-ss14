// The pilot's HUD and weapon keys: while you're in a mech, 1/2/3 jump to that mounted weapon instead of the action bar.
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Input;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.Mech;

public sealed class FSMechHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    public const int WeaponKeys = 3;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSMechHudOverlay());

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
