using Content.Client.UserInterface.Fragments;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.ReadyCheck;

public sealed partial class ReadyCheckPDAUi : UIFragment
{
    private ReadyCheckPDAFragment? _fragment;

    public override Control GetUIFragmentRoot() => _fragment!;

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new ReadyCheckPDAFragment();
        _fragment.OnStatusPressed += status =>
        {
            var msg = new ReadyCheckUiMessageEvent(status);
            userInterface.SendMessage(new CartridgeUiMessage(msg));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ReadyCheckPDAUiState pdaState) return;
        _fragment?.UpdateState(pdaState);
    }
}
