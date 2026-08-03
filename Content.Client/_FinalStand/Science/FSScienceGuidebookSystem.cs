using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._FinalStand.Science;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Science;

// Auto-opens the guidebook to the Science section the first time this client spawns as a Scientist - onboards the Harvester/research loop for players new to (or drafted into) the department.
public sealed class FSScienceGuidebookSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private static readonly ProtoId<GuideEntryPrototype> ScienceGuide = "Science";

    private bool _shown;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSPlayerScienceStatusEvent>(OnScienceStatus);
    }

    private void OnScienceStatus(FSPlayerScienceStatusEvent ev)
    {
        if (!ev.IsScience || _shown)
            return;

        _shown = true;
        _ui.GetUIController<GuidebookUIController>().OpenGuidebook(selected: ScienceGuide);
    }
}
