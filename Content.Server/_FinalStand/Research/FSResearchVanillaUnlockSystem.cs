using Content.Server.Research.Systems;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// Grants a wrapper fsTechNode's linked vanilla technology via vanilla's own recipe/generic-unlock plumbing on completion.
public sealed partial class FSResearchVanillaUnlockSystem : EntitySystem
{
    [Dependency] private FSResearchSystem _fsResearch = default!;
    [Dependency] private ResearchSystem _vanillaResearch = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
    }

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (!_prototype.TryIndex<FSTechNodePrototype>(ev.NodeId, out var node) || node.VanillaTechnologyId is not { } vanillaId)
            return;

        var station = _fsResearch.GetOrCreateStation();

        // The station can be a bare nullspace fallback, which AddTechnology only logs about.
        EnsureComp<TechnologyDatabaseComponent>(station.Owner);
        _vanillaResearch.AddTechnology(station.Owner, vanillaId);
    }
}
