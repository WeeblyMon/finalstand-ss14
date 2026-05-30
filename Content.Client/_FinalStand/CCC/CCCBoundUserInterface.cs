using Content.Client._FinalStand.CCC.UI;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CCCWindow? _window;

    public CCCBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CCCWindow>();
        _window.OnStartWavePressed += () => SendMessage(new CCCStartWaveMessage());
        _window.OnBroadcastPressed += text => SendMessage(new CCCBroadcastMessage(text));
        _window.OnClose += Close;

        EntityUid? gridUid = null;
        if (EntMan.TryGetComponent<TransformComponent>(Owner, out var xform))
            gridUid = xform.GridUid;

        _window.InitMaps(gridUid, Owner);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not CCCBoundUserInterfaceState cccState) return;
        _window?.UpdateState(cccState, IsLocalPlayerCaptain());
    }

    private bool IsLocalPlayerCaptain()
    {
        var player = IoCManager.Resolve<IPlayerManager>().LocalSession;
        if (player?.AttachedEntity is not { } mob) return false;
        var mind = EntMan.System<SharedMindSystem>();
        var jobs = EntMan.System<SharedJobSystem>();
        if (!mind.TryGetMind(mob, out var mindId, out _)) return false;
        return jobs.MindTryGetJob(mindId, out var job) && ReadyCheckDepts.IsCaptain(job.ID);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _window?.Dispose();
    }
}
