using Content.Shared._FinalStand.CryoSleep;

namespace Content.Client._FinalStand.CryoSleep;

public sealed partial class FSCryoEnterPromptSystem : EntitySystem
{
    private FSConfirmWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSCryoEnterPromptEvent>(OnPrompt);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    private void OnPrompt(FSCryoEnterPromptEvent ev)
    {
        _window?.Close();
        _window = new FSConfirmWindow(
            Loc.GetString("fs-cryo-enter-window-title"),
            Loc.GetString("fs-cryo-enter-window-prompt"),
            Loc.GetString("fs-cryo-enter-window-accept"),
            Loc.GetString("fs-cryo-enter-window-deny"));

        var pod = ev.Pod;
        _window.OnAccepted += () => RaiseNetworkEvent(new FSCryoEnterConfirmEvent(pod));
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
}
