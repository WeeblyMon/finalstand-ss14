using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.MedicalOps;

// Z on the chem launcher: a radial wheel over its loaded formulas.
public sealed partial class FSChemFormulaWheel : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private SimpleRadialMenu? _menu;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FSChemFormulaWheelEvent>(OnOpen);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Close();
    }

    private void Close()
    {
        _menu?.Close();
        _menu = null;
    }

    private void OnOpen(FSChemFormulaWheelEvent ev)
    {
        Close();

        if (_player.LocalEntity is not { } user
            || !TryComp<FSChemLauncherComponent>(GetEntity(ev.Launcher), out var launcher)
            || launcher.Slots.Count < 2)
            return;

        var options = new List<RadialMenuOptionBase>(launcher.Slots.Count);
        for (var i = 0; i < launcher.Slots.Count; i++)
        {
            if (!_prototypes.TryIndex(launcher.Slots[i], out var formula))
                continue;

            var name = Loc.GetString(formula.Name);
            options.Add(new RadialMenuActionOption<int>(Select, i)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(formula.Canister),
                ToolTip = $"{name} ({formula.Cost} reserve)" + (i == launcher.Selected ? " - loaded" : string.Empty),
            });
        }

        _menu = _ui.CreateWindow<SimpleRadialMenu>();
        _menu.Track(user);
        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();

        void Select(int slot)
        {
            RaiseNetworkEvent(new FSChemLauncherSelectMessage(slot));
            Close();
        }
    }
}
