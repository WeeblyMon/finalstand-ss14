using Content.Server.Research.Systems;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Research.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// Grants a wrapper fsTechNode's linked vanilla technology to every research server on completion.
public sealed partial class FSResearchVanillaUnlockSystem : EntitySystem
{
    [Dependency] private FSResearchSystem _fsResearch = default!;
    [Dependency] private FSMedicalResearchSystem _medicalResearch = default!;
    [Dependency] private ResearchSystem _vanillaResearch = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerInit);
    }

    private void OnNodeCompleted(ref FSResearchNodeCompletedEvent ev)
    {
        if (!TryGetVanillaId(ev.NodeId, out var vanillaId))
            return;

        GrantToAllServers(vanillaId);
    }

    // A server built mid-round gets everything already researched, the medical branch included (it's bought
    // through its own system, not station research).
    private void OnServerInit(Entity<ResearchServerComponent> ent, ref MapInitEvent args)
    {
        if (_fsResearch.TryGetStation(out var station))
        {
            foreach (var nodeId in station.Comp.UnlockedNodes)
                GrantNode(ent.Owner, nodeId);
        }

        foreach (var nodeId in _medicalResearch.UnlockedNodeIds())
            GrantNode(ent.Owner, nodeId);
    }

    private void GrantNode(EntityUid server, string nodeId)
    {
        if (TryGetVanillaId(nodeId, out var vanillaId))
            GrantToServer(server, vanillaId);
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
