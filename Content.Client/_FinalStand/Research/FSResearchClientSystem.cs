using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Systems;

namespace Content.Client._FinalStand.Research;

// Notifies the open research window when FSTechDatabaseComponent changes server-side.
public sealed class FSResearchClientSystem : SharedFSResearchSystem
{
    public event Action<EntityUid>? DatabaseUpdated;
    public event Action<string>? AuthorityDenied;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSTechDatabaseComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeNetworkEvent<FSResearchAuthorityDeniedEvent>(OnAuthorityDenied);
    }

    private void OnAfterHandleState(EntityUid uid, FSTechDatabaseComponent component, ref AfterAutoHandleStateEvent args)
    {
        DatabaseUpdated?.Invoke(uid);
    }

    private void OnAuthorityDenied(FSResearchAuthorityDeniedEvent ev)
    {
        AuthorityDenied?.Invoke(ev.Reason);
    }
}
