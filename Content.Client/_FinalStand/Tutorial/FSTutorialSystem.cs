using Content.Shared._FinalStand.Tutorial;
using Content.Shared.Administration;
using Robust.Client.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Tutorial;

public sealed partial class FSTutorialSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private FSTutorialWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSTutorialShowEvent>(OnShow);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    public void Request()
    {
        RaiseNetworkEvent(new FSTutorialRequestEvent());
    }

    private void OnShow(FSTutorialShowEvent ev)
    {
        Open(ev.Tutorial);
    }

    public void Open(string id)
    {
        if (!ProtoMan.TryIndex<FSTutorialPrototype>(id, out var tutorial) || tutorial.Pages.Count == 0)
            return;

        _window?.Close();
        _window = new FSTutorialWindow(tutorial, _sprite);
        _window.OnClose += () =>
        {
            if (_window?.DontShowAgain.Pressed == true)
                RaiseNetworkEvent(new FSTutorialSeenEvent(tutorial.ID));
            _window = null;
        };
        _window.OpenCentered();
    }
}

// Any player: the escape menu's Tutorial button runs it.
[AnyCommand]
public sealed partial class FSTutorialCommand : LocalizedEntityCommands
{
    [Dependency] private FSTutorialSystem _tutorial = default!;

    public override string Command => "fstutorial";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 0)
            _tutorial.Open(args[0]);
        else
            _tutorial.Request();
    }
}
