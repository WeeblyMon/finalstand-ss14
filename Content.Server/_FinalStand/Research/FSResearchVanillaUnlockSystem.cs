using Content.Server.Research.Systems;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Research.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// Grants a wrapper fsTechNode's linked vanilla technology to every research server on completion.
public sealed partial class FSResearchVanillaUnlockSystem : EntitySystem
{
    [Dependency] private FSResearchSystem _fsResearch = default!;
    [Dependency] private ResearchSystem _vanillaResearch = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerInit);
    }

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (!TryGetVanillaId(ev.NodeId, out var vanillaId))
            return;

        GrantToAllServers(vanillaId);
    }

    private void OnServerInit(Entity<ResearchServerComponent> ent, ref MapInitEvent args)
    {
        if (!_fsResearch.TryGetStation(out var station))
            return;

        foreach (var nodeId in station.Comp.UnlockedNodes)
        {
            if (TryGetVanillaId(nodeId, out var vanillaId))
                GrantToServer(ent.Owner, vanillaId);
        }
    }

    private void GrantToAllServers(string vanillaId)
    {
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var uid, out _))
            GrantToServer(uid, vanillaId);
    }

    private void GrantToServer(EntityUid server, string vanillaId)
    {
        EnsureComp<TechnologyDatabaseComponent>(server);
        _vanillaResearch.AddTechnology(server, vanillaId);
    }

    private bool TryGetVanillaId(string nodeId, out string vanillaId)
    {
        vanillaId = string.Empty;

        if (!_prototype.TryIndex<FSTechNodePrototype>(nodeId, out var node) || node.VanillaTechnologyId is not { } id)
            return false;

        vanillaId = id;
        return true;
    }
}
