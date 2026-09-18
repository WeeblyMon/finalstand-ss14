using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._FinalStand.Departments;
using Content.Shared.Guidebook;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Science;

// Auto-opens the guidebook to the Science section the first time this client spawns as a Scientist - onboards the Harvester/research loop for players new to (or drafted into) the department.
public sealed partial class FSScienceGuidebookSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly ProtoId<GuideEntryPrototype> ScienceGuide = "Science";

    private bool _shown;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDepartmentAccessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSDepartmentAccessComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStartup(Entity<FSDepartmentAccessComponent> ent, ref ComponentStartup args)
    {
        TryShow(ent);
    }

    private void OnStateHandled(Entity<FSDepartmentAccessComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        TryShow(ent);
    }

    private void TryShow(Entity<FSDepartmentAccessComponent> ent)
    {
        if (_shown || !ent.Comp.Science || _player.LocalEntity != ent.Owner)
            return;

        _shown = true;
        _ui.GetUIController<GuidebookUIController>().OpenGuidebook(selected: ScienceGuide);
    }
}
