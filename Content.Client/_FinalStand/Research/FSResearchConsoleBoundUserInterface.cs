using Content.Client._FinalStand.Research.UI;
using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Research;

[UsedImplicitly]
public sealed class FSResearchConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private FSResearchTreeMenu? _consoleMenu;

    private Action<EntityUid>? _onDatabaseUpdated;
    private Action<string>? _onAuthorityDenied;

    public FSResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var owner = Owner;

        _consoleMenu = this.CreateWindow<FSResearchTreeMenu>();
        _consoleMenu.SetEntity(owner);

        _consoleMenu.OnTechnologyCardPressed += id =>
        {
            SendMessage(new ConsoleUnlockTechnologyMessage(id));
        };

        _consoleMenu.OnFsNodeSelected += id =>
        {
            SendMessage(new FSSelectResearchNodeMessage(id));
        };

        _consoleMenu.OnServerButtonPressed += () =>
        {
            SendMessage(new ConsoleServerSelectionMessage());
        };

        // FSTechDatabaseComponent changes don't push a BUI state, so wire this manually.
        var researchClient = EntMan.System<FSResearchClientSystem>();
        _onDatabaseUpdated = uid =>
        {
            if (uid == owner)
                _consoleMenu?.RefreshLiveState();
        };
        researchClient.DatabaseUpdated += _onDatabaseUpdated;

        _onAuthorityDenied = reason => _consoleMenu?.ShowAuthorityDenied(reason);
        researchClient.AuthorityDenied += _onAuthorityDenied;
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if (!args.WasModified<TechnologyPrototype>() && !args.WasModified<FSTechNodePrototype>())
            return;

        if (State is not ResearchConsoleBoundInterfaceState rState)
            return;

        _consoleMenu?.UpdatePanels(rState);
        _consoleMenu?.UpdateInformationPanel(rState);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ResearchConsoleBoundInterfaceState castState)
            return;
        _consoleMenu?.UpdatePanels(castState);
        _consoleMenu?.UpdateInformationPanel(castState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        var researchClient = EntMan.System<FSResearchClientSystem>();
        if (_onDatabaseUpdated != null)
            researchClient.DatabaseUpdated -= _onDatabaseUpdated;
        if (_onAuthorityDenied != null)
            researchClient.AuthorityDenied -= _onAuthorityDenied;
    }
}
