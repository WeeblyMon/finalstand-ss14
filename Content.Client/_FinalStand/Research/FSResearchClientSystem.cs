using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Research.Systems;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Research;

// Notifies the open research window when FSTechDatabaseComponent changes server-side, or the viewer's own personal pick changes.
public sealed class FSResearchClientSystem : SharedFSResearchSystem
{
    public event Action<EntityUid>? DatabaseUpdated;
    public event Action<string>? AuthorityDenied;
    public event Action? PersonalPickChanged;
    public event Action? SharedResearchChanged;

    public ProtoId<FSTechNodePrototype>? MyPersonalPickId { get; private set; }
    public int MyPersonalProgress { get; private set; }
    public List<string> MyPersonalQueue { get; private set; } = new();

    public bool IsRdOrCaptain { get; private set; }

    public ProtoId<FSTechNodePrototype>? SharedResearchId { get; private set; }
    public int SharedResearchProgress { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSTechDatabaseComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeNetworkEvent<FSResearchAuthorityDeniedEvent>(OnAuthorityDenied);
        SubscribeNetworkEvent<FSPersonalResearchStateEvent>(OnPersonalResearchState);
        SubscribeNetworkEvent<FSPlayerResearchAuthorityEvent>(OnResearchAuthority);
        SubscribeNetworkEvent<FSSharedResearchStateEvent>(OnSharedResearchState);
    }

    private void OnAfterHandleState(EntityUid uid, FSTechDatabaseComponent component, ref AfterAutoHandleStateEvent args)
    {
        DatabaseUpdated?.Invoke(uid);
    }

    private void OnAuthorityDenied(FSResearchAuthorityDeniedEvent ev)
    {
        AuthorityDenied?.Invoke(ev.Reason);
    }

    private void OnPersonalResearchState(FSPersonalResearchStateEvent ev)
    {
        MyPersonalPickId = ev.NodeId;
        MyPersonalProgress = ev.Progress;
        MyPersonalQueue = ev.Queue;
        PersonalPickChanged?.Invoke();
    }

    private void OnResearchAuthority(FSPlayerResearchAuthorityEvent ev)
    {
        IsRdOrCaptain = ev.IsRdOrCaptain;
    }

    private void OnSharedResearchState(FSSharedResearchStateEvent ev)
    {
        SharedResearchId = ev.NodeId;
        SharedResearchProgress = ev.Progress;
        SharedResearchChanged?.Invoke();
    }
}
