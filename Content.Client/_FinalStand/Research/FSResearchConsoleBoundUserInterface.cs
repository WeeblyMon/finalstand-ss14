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
    private FSResearchTreeMenu? _menu;

    private FSResearchClientSystem? _research;

    public FSResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        if (_menu != null)
            return;

        _menu = this.CreateWindow<FSResearchTreeMenu>();
        _menu.SetEntity(Owner);

        _menu.OnFsNodeSelected += id => SendMessage(new FSSelectResearchNodeMessage(id));
        _menu.OnFsNodeQueued += id => SendMessage(new FSEnqueueResearchNodeMessage(id));
        _menu.OnFsNodeDequeued += id => SendMessage(new FSDequeueResearchNodeMessage(id));
        _menu.OnServerButtonPressed += () => SendMessage(new ConsoleServerSelectionMessage());
        _menu.OnClearPersonalPick += () => SendMessage(new FSClearPersonalResearchMessage());
        _menu.OnClearSharedPick += () => SendMessage(new FSClearSharedResearchMessage());

        _research = EntMan.System<FSResearchClientSystem>();
        _research.DatabaseUpdated += OnDatabaseUpdated;
        _research.AuthorityDenied += OnAuthorityDenied;
        _research.PersonalPickChanged += Refresh;
        _research.SharedResearchChanged += Refresh;
        _research.ContributionReceived += OnContribution;
    }

    private void OnDatabaseUpdated(EntityUid uid)
    {
        if (uid == Owner)
            Refresh();
    }

    private void OnAuthorityDenied(string reason) => _menu?.ShowAuthorityDenied(reason);

    private void OnContribution(int contributed, int earned) => _menu?.SetContribution(contributed, earned);

    private void Refresh() => _menu?.RefreshLiveState();

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if (args.WasModified<TechnologyPrototype>() || args.WasModified<FSTechNodePrototype>())
            _menu?.InvalidateLayout();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ResearchConsoleBoundInterfaceState)
            Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _research == null)
            return;

        _research.DatabaseUpdated -= OnDatabaseUpdated;
        _research.AuthorityDenied -= OnAuthorityDenied;
        _research.PersonalPickChanged -= Refresh;
        _research.SharedResearchChanged -= Refresh;
        _research.ContributionReceived -= OnContribution;
    }
}
