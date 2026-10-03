using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Fortifications;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Fortifications;

public sealed class FSUpgradeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly Color Unavailable = Color.FromHex("#3a3a3a").WithAlpha(0.6f);
    private static readonly Color Pending = Color.FromHex("#d9a441").WithAlpha(0.45f);

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<FSUpgradeableComponent>(Owner, out var comp))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(CreateButtons(comp));
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons(FSUpgradeableComponent comp)
    {
        var rules = EntMan.System<FSUpgradeSystem>();
        foreach (var id in comp.Options)
        {
            var upgrade = _proto.Index(id);
            var block = rules.Check(comp, upgrade);
            var tooltip = Loc.GetString("fs-upgrade-tooltip",
                ("name", Loc.GetString(upgrade.Name)),
                ("desc", Loc.GetString(upgrade.Description)),
                ("cost", CostText(upgrade)),
                ("hits", upgrade.Hits));

            if (block != FSUpgradeBlock.None)
                tooltip += "\n" + Loc.GetString("fs-upgrade-blocked-" + block.ToString().ToLowerInvariant());

            yield return new RadialMenuActionOption<ProtoId<FSUpgradePrototype>>(Select, id)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(upgrade.Icon),
                ToolTip = tooltip,
                BackgroundColor = block != FSUpgradeBlock.None ? Unavailable : comp.Pending == id ? Pending : null,
            };
        }
    }

    private string CostText(FSUpgradePrototype upgrade)
    {
        var parts = new List<string>();
        foreach (var (material, amount) in upgrade.Cost)
            parts.Add($"{amount} {Loc.GetString(_proto.Index(material).Name)}");

        return string.Join(", ", parts);
    }

    private void Select(ProtoId<FSUpgradePrototype> upgrade)
    {
        SendMessage(new FSUpgradeSelectMessage(upgrade));
    }
}
