using Content.Shared._FinalStand.Sprint;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client._FinalStand.Sprint;

public sealed class FSSprintClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.FSSprint,
                InputCmdHandler.FromDelegate(OnSprintKeyDown, OnSprintKeyUp))
            .Register<FSSprintClientSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<FSSprintClientSystem>();
    }

    private void OnSprintKeyDown(ICommonSession? session)
    {
        RaiseNetworkEvent(new FSSprintStartMessage());
    }

    private void OnSprintKeyUp(ICommonSession? session)
    {
        RaiseNetworkEvent(new FSSprintStopMessage());
    }
}
