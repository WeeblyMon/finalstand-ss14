using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Titles;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Profile;

public sealed partial class FSProfileUIController : UIController, IOnSystemChanged<FSUiEventRelaySystem>
{
    [Dependency] private IPrototypeManager _proto = default!;

    private FSTitlesWindow? _titles;
    private FSMentorWindow? _mentor;

    public ProtoId<FSTitlePrototype>? EquippedTitle { get; private set; }

    public event Action? EquippedTitleChanged;

    public void OnSystemLoaded(FSUiEventRelaySystem system)
    {
        system.TitlesUpdated += OnTitles;
        system.MentorUpdated += OnMentor;
    }

    public void OnSystemUnloaded(FSUiEventRelaySystem system)
    {
        system.TitlesUpdated -= OnTitles;
        system.MentorUpdated -= OnMentor;
    }

    public void RequestTitles() => EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSTitlesRequestEvent());

    public void OpenTitles()
    {
        if (_titles is not { Disposed: false })
        {
            _titles = new FSTitlesWindow();
            _titles.OnEquip += title => EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSTitleEquipEvent { Title = title });
        }

        _titles.OpenCentered();
        _titles.MoveToFront();
        RequestTitles();
    }

    public void OpenMentor()
    {
        if (_mentor is not { Disposed: false })
        {
            _mentor = new FSMentorWindow();
            _mentor.OnBuy += item => EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSMentorBuyEvent { Item = item });
        }

        _mentor.OpenCentered();
        _mentor.MoveToFront();
        EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSMentorRequestEvent());
    }

    public (string? Name, Color Color) EquippedTitleDisplay()
    {
        return EquippedTitle is { } equipped && _proto.TryIndex(equipped, out var title)
            ? (Loc.GetString(title.Name), title.Color)
            : (null, Color.White);
    }

    private void OnTitles(FSTitlesStateEvent state)
    {
        if (EquippedTitle != state.Equipped)
        {
            EquippedTitle = state.Equipped;
            EquippedTitleChanged?.Invoke();
        }

        if (_titles is { Disposed: false, IsOpen: true })
            _titles.Update(state);
    }

    private void OnMentor(FSMentorStateEvent state)
    {
        if (_mentor is { Disposed: false, IsOpen: true })
            _mentor.Update(state);
    }
}
