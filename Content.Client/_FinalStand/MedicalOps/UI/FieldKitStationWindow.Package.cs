using Content.Client._FinalStand.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.MedicalOps.UI;

public sealed partial class FieldKitStationWindow
{
    private const int BottleVolume = 30;
    private const int MaxBottles = 10;
    private const int MaxPills = 20;
    private static readonly int[] Doses = { 5, 10, 15, 30 };

    private int _dose = 10;

    private void RefreshPackage()
    {
        PackRow.RemoveAllChildren();
        PackRow.AddChild(BottleCard());
        PackRow.AddChild(PillCard());
    }

    private (PanelContainer Card, BoxContainer Body) Card(string title, string tag)
    {
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = Box(FSUiPalette.BgDeep, FSUiPalette.BgTrack, new Thickness(1)),
        };

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(14, 10) };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        head.AddChild(new Label { Text = title, FontOverride = _bold, FontColorOverride = FSUiPalette.TextPrimary, HorizontalExpand = true });
        var chip = new PanelContainer { VerticalAlignment = VAlignment.Center, PanelOverride = Box(Color.Transparent, FSUiPalette.BorderNeutral, new Thickness(1), 5, 0) };
        chip.AddChild(new Label { Text = tag, FontOverride = _caption, FontColorOverride = FSUiPalette.TextMuted });
        head.AddChild(chip);
        body.AddChild(head);
        card.AddChild(body);
        return (card, body);
    }

    private Control BottleCard()
    {
        var (card, body) = Card("Bottles", "FOR DOCTORS");
        var count = Math.Min(MaxBottles, (int) Math.Ceiling(JugVolume.Float() / BottleVolume));
        body.AddChild(new Label { Text = $"{BottleVolume}u each · uses the label", FontColorOverride = FSUiPalette.TextMuted });
        var button = ActionButton(count <= 0 ? "JUG EMPTY" : $"BOTTLE {count}", count > 0, false, 34);
        button.OnPressed += _ => OnBottle?.Invoke(count);
        body.AddChild(button);
        return card;
    }

    private Control PillCard()
    {
        var (card, body) = Card("Pills", "FOR DOCTORS");
        var doses = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        foreach (var dose in Doses)
        {
            doses.AddChild(Option($"{dose}u", "", _dose == dose, true, () =>
            {
                _dose = dose;
                RefreshPackage();
            }, 52));
        }
        body.AddChild(doses);

        var count = Math.Min(MaxPills, (int) (JugVolume.Float() / _dose));
        var button = ActionButton(count <= 0 ? "NOT ENOUGH IN THE JUG" : $"PRESS {count} × {_dose}u", count > 0, false, 34);
        button.OnPressed += _ => OnPill?.Invoke(_dose, count);
        body.AddChild(button);
        return card;
    }
}
