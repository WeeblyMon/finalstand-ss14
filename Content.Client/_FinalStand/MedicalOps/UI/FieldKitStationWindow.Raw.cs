using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared.Chemistry;
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

    private void RefreshRaw()
    {
        RawGroups.RemoveAllChildren();
        var filter = SearchBox.Text.Trim();

        var jugReady = _state?.Jug != null && JugFree > 0;
        var tiles = new List<Control>();
        foreach (var item in (_state?.Inventory ?? new List<ReagentInventoryItem>()).OrderBy(i => i.ReagentLabel))
        {
            var name = item.ReagentId != null ? ReagentName(item.ReagentId) : item.ReagentLabel;
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var location = item.StorageLocation;
            tiles.Add(ReagentTile(name, item.ReagentColor.WithAlpha(1f), $"{item.Quantity}u left",
                jugReady && item.Quantity > 0, () => OnDispense?.Invoke(location, _amount)));
        }

        if (tiles.Count == 0)
        {
            RawGroups.AddChild(Muted(filter.Length > 0 ? $"No reagent matches \"{filter}\"." : "The station's stock is empty."));
            return;
        }

        var group = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var title = new Label { Text = "STOCK" };
        Caption(title);
        head.AddChild(title);
        head.AddChild(new Label { Text = "For doctors' chems and anything off-book", FontColorOverride = Dim });
        group.AddChild(head);

        var grid = new GridContainer { Columns = 4, HSeparationOverride = 6, VSeparationOverride = 6 };
        foreach (var tile in tiles)
            grid.AddChild(tile);
        group.AddChild(grid);
        RawGroups.AddChild(group);
    }

    private ContainerButton ReagentTile(string name, Color color, string sub, bool enabled, Action onPressed)
    {
        var button = new ContainerButton
        {
            HorizontalExpand = true,
            MinSize = new Vector2(150, 46),
            Disabled = !enabled,
            Modulate = enabled ? Color.White : Color.White.WithAlpha(0.55f),
            StyleBoxOverride = Box(FSUiPalette.BgSurface, FSUiPalette.BgTrack, new Thickness(1), 6, 0),
            ToolTip = enabled ? $"Dispense {_amount}u into the jug" : null,
        };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10, VerticalAlignment = VAlignment.Center };
        row.AddChild(Swatch(color, 10, 26));
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        text.AddChild(new Label { Text = name, FontOverride = _bold, FontColorOverride = FSUiPalette.TextPrimary, ClipText = true, HorizontalExpand = true });
        text.AddChild(new Label { Text = sub, FontOverride = _caption, FontColorOverride = Dim });
        row.AddChild(text);
        button.AddChild(row);
        button.OnPressed += _ => onPressed();
        return button;
    }
}
