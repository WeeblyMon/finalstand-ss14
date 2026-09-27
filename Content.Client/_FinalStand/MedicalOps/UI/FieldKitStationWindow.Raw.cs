using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.MedicalOps.UI;

public sealed partial class FieldKitStationWindow
{
    private static readonly int[] Amounts = { 1, 5, 10, 15, 25, 50, 100 };

    private int _amount = 10;

    private void BuildAmountRow()
    {
        AmountRow.RemoveAllChildren();
        foreach (var amount in Amounts)
        {
            AmountRow.AddChild(Option($"{amount}u", "", _amount == amount, true, () =>
            {
                _amount = amount;
                BuildAmountRow();
                RefreshRaw();
            }, 52));
        }
    }

    // Reagents a field kit needs besides tissue, so they can be grouped apart from the general elements.
    private HashSet<string> KitParts()
    {
        var parts = new HashSet<string>();
        foreach (var kit in _prototype.EnumeratePrototypes<FSFieldKitPrototype>())
        {
            if (kit.Reaction is not { } id || !_prototype.TryIndex(id, out ReactionPrototype? reaction))
                continue;
            foreach (var reagent in reaction.Reactants.Keys)
            {
                if (reagent != TissueReagent)
                    parts.Add(reagent);
            }
        }
        return parts;
    }

    private void RefreshRaw()
    {
        RawGroups.RemoveAllChildren();
        var filter = SearchBox.Text.Trim();
        bool Matches(string name) => filter.Length == 0 || name.Contains(filter, StringComparison.OrdinalIgnoreCase);

        var jugReady = _state?.Jug != null && JugFree > 0;
        var parts = KitParts();
        var inventory = _state?.Inventory ?? new List<ReagentInventoryItem>();

        var tissue = new List<Control>();
        if (Matches("Tissue"))
        {
            var tank = _state?.Tank ?? FixedPoint2.Zero;
            tissue.Add(ReagentTile("Tissue", Tissue, $"{tank}u in tank", OliveText, jugReady && tank > 0, true, () => OnDispense?.Invoke(null, _amount)));
        }

        var kitTiles = new List<Control>();
        var otherTiles = new List<Control>();
        foreach (var item in inventory.OrderBy(i => i.ReagentLabel))
        {
            var name = item.ReagentId != null ? ReagentName(item.ReagentId) : item.ReagentLabel;
            if (!Matches(name))
                continue;

            var location = item.StorageLocation;
            var tile = ReagentTile(name, item.ReagentColor.WithAlpha(1f), $"{item.Quantity}u left", Dim,
                jugReady && item.Quantity > 0, false, () => OnDispense?.Invoke(location, _amount));
            (item.ReagentId != null && parts.Contains(item.ReagentId) ? kitTiles : otherTiles).Add(tile);
        }

        var stocked = inventory.Where(i => i.ReagentId != null).Select(i => i.ReagentId!).ToHashSet();
        foreach (var missing in parts.Where(p => !stocked.Contains(p)).OrderBy(ReagentName))
        {
            var name = ReagentName(missing);
            if (Matches(name))
                kitTiles.Add(ReagentTile(name, ReagentColor(missing), "print at the lathe", Research, false, false, () => { }));
        }

        AddGroup("TISSUE", "Straight from the station's tank", tissue);
        AddGroup("FIELD KIT PARTS", "The second half of every potion", kitTiles);
        AddGroup("ELEMENTS", "For doctors' chems and anything off-book", otherTiles);

        if (RawGroups.ChildCount == 0)
            RawGroups.AddChild(Muted($"No reagent matches \"{filter}\"."));
    }

    private void AddGroup(string title, string hint, List<Control> tiles)
    {
        if (tiles.Count == 0)
            return;

        var group = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var titleLabel = new Label { Text = title };
        Caption(titleLabel);
        head.AddChild(titleLabel);
        head.AddChild(new Label { Text = hint, FontColorOverride = Dim });
        group.AddChild(head);

        var grid = new GridContainer { Columns = 4, HSeparationOverride = 6, VSeparationOverride = 6 };
        foreach (var tile in tiles)
            grid.AddChild(tile);
        group.AddChild(grid);
        RawGroups.AddChild(group);
    }

    private ContainerButton ReagentTile(string name, Color color, string sub, Color subColor, bool enabled, bool tank, Action onPressed)
    {
        var button = new ContainerButton
        {
            HorizontalExpand = true,
            MinSize = new Vector2(150, 46),
            Disabled = !enabled,
            Modulate = enabled ? Color.White : Color.White.WithAlpha(0.55f),
            StyleBoxOverride = Box(tank ? Color.FromHex("#1c2218") : FSUiPalette.BgSurface, tank ? Color.FromHex("#3e4a2f") : FSUiPalette.BgTrack, new Thickness(1), 6, 0),
            ToolTip = enabled ? $"Dispense {_amount}u into the jug" : null,
        };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10, VerticalAlignment = VAlignment.Center };
        row.AddChild(Swatch(color, 10, 26));
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        text.AddChild(new Label { Text = name, FontOverride = _bold, FontColorOverride = FSUiPalette.TextPrimary, ClipText = true, HorizontalExpand = true });
        text.AddChild(new Label { Text = sub, FontOverride = _caption, FontColorOverride = subColor });
        row.AddChild(text);
        button.AddChild(row);
        button.OnPressed += _ => onPressed();
        return button;
    }
}
