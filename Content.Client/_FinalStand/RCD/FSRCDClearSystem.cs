using Content.Client.ContextMenu.UI;
using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.RCD;

public sealed class FSRCDClearSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.UseSecondary, new ClearHandler(this), typeof(EntityMenuUIController))
            .Register<FSRCDClearSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<FSRCDClearSystem>();
    }

    private bool TryClear()
    {
        if (_player.LocalEntity is not { } player
            || _ui.CurrentlyHovered is not IViewportControl
            || !_hands.TryGetActiveItem(player, out var held)
            || !HasComp<FSEngineerOnlyRCDComponent>(held)
            || !TryComp<RCDComponent>(held, out var rcd)
            || rcd.ProtoId == FSRCDEmpty.Piece)
            return false;

        RaiseNetworkEvent(new FSRCDBeltPickEvent(GetNetEntity(held.Value), FSRCDEmpty.Piece));
        _placement.Clear();
        return true;
    }

    private sealed class ClearHandler(FSRCDClearSystem system) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            return message.State == BoundKeyState.Down && system.TryClear();
        }
    }
}
