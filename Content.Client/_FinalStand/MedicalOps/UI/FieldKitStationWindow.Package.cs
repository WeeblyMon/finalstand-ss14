using System.Linq;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared.FixedPoint;
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

    private FixedPoint2? _flaskCapacity;
    private int _flaskCount = 1;
    private int _dose = 10;

    private void RefreshPackage()
    {
        var jug = _state?.Jug;
        FillBar(PackJugBar, jug, null);
        PackJugLabel.Text = jug == null ? "No jug" : jug.CurrentVolume <= 0 ? "Jug empty" : $"{jug.CurrentVolume}u";

        PackGrid.RemoveAllChildren();
        PackGrid.AddChild(MagazineCard());
        PackGrid.AddChild(FlaskCard());
        PackGrid.AddChild(BottleCard());
        PackGrid.AddChild(PillCard());
    }

    private (PanelContainer Card, BoxContainer Body) Card(string title, string tag, bool major)
    {
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = Box(major ? FSUiPalette.BgSurface : FSUiPalette.BgDeep, FSUiPalette.BgTrack, new Thickness(1)),
        };
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        if (major)
            column.AddChild(new PanelContainer { SetHeight = 3, PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.Currency } });
        card.AddChild(column);

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10, Margin = new Thickness(16, 14), VerticalExpand = true };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        head.AddChild(new Label { Text = title, FontOverride = major ? _title : _bold, FontColorOverride = FSUiPalette.TextPrimary, HorizontalExpand = true });
        var chip = new PanelContainer { VerticalAlignment = VAlignment.Center, PanelOverride = Box(Color.Transparent, major ? Color.FromHex("#5c4a26") : FSUiPalette.BorderNeutral, new Thickness(1), 5, 0) };
        chip.AddChild(new Label { Text = tag, FontOverride = _caption, FontColorOverride = major ? FSUiPalette.Currency : FSUiPalette.TextMuted });
        head.AddChild(chip);
        body.AddChild(head);
        column.AddChild(body);
        return (card, body);
    }

    private Control MagazineCard()
    {
        var (card, body) = Card("Syringe magazine", "DARTS", true);
        var slot = new PanelContainer { PanelOverride = Box(Color.FromHex("#0e0d0c"), _state?.MagazineName != null ? FSUiPalette.BorderSubtle : FSUiPalette.BorderNeutral, new Thickness(1), 12, 8) };
        var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };

        var hasMagazine = _state?.MagazineName != null;
        text.AddChild(new Label { Text = hasMagazine ? _state!.MagazineName! : "No magazine inserted", FontOverride = _bold, FontColorOverride = FSUiPalette.TextPrimary, ClipText = true, HorizontalExpand = true });
        text.AddChild(new Label
        {
            Text = hasMagazine ? $"{_state!.MagazineVolume} / {_state.MagazineMax} darts" : "Use a magazine on the station to insert it.",
            FontOverride = _caption,
            FontColorOverride = FSUiPalette.TextMuted,
        });
        line.AddChild(text);
        if (hasMagazine)
        {
            var eject = ActionButton("EJECT", true, false, 28);
            eject.OnPressed += _ => OnEject?.Invoke(true);
            line.AddChild(eject);
        }
        slot.AddChild(line);
        body.AddChild(slot);
        body.AddChild(Wrapped("One dart is 1u. The gun trims every dose to what the target can safely take.", FSUiPalette.TextMuted, 380));
        body.AddChild(new Control { VerticalExpand = true });

        var room = hasMagazine ? _state!.MagazineMax - _state.MagazineVolume : FixedPoint2.Zero;
        var load = FixedPoint2.Min(room, JugVolume);
        var (label, enabled) = !hasMagazine ? ("INSERT A MAGAZINE", false)
            : room <= 0 ? ("MAGAZINE FULL", false)
            : JugVolume <= 0 ? ("JUG EMPTY", false)
            : ($"LOAD {load} DARTS", true);
        var button = ActionButton(label, enabled, true, 42);
        button.OnPressed += _ =>
        {
            OnLoadMagazine?.Invoke();
            Log($"{load} darts loaded");
        };
        body.AddChild(button);
        return card;
    }

    private Control FlaskCard()
    {
        var (card, body) = Card("Splash flasks", "THROWN", true);
        var stocks = _state?.Flasks ?? new();
        if (_flaskCapacity == null || stocks.All(s => s.Capacity != _flaskCapacity))
            _flaskCapacity = stocks.FirstOrDefault()?.Capacity;

        var sizes = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        if (stocks.Count == 0)
            sizes.AddChild(Muted("The rack is empty. Use empty flasks on the station to load it.", 380));
        foreach (var stock in stocks)
        {
            var capacity = stock.Capacity;
            sizes.AddChild(Option(capacity > 50 ? "HEAVY" : "SPLASH", $"{capacity}u · {stock.Count} in rack", _flaskCapacity == capacity, true, () =>
            {
                _flaskCapacity = capacity;
                RefreshPackage();
            }, 150));
        }
        body.AddChild(sizes);

        var selected = stocks.FirstOrDefault(s => s.Capacity == _flaskCapacity);
        var max = selected == null ? 0 : Math.Min(selected.Count, (int) (JugVolume.Float() / selected.Capacity.Float()));
        _flaskCount = Math.Clamp(_flaskCount, Math.Min(1, max), Math.Max(max, 0));

        var stepper = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        stepper.AddChild(new Label { Text = "How many", HorizontalExpand = true, VerticalAlignment = VAlignment.Center });
        stepper.AddChild(Step("−", _flaskCount > 1, () => _flaskCount--));
        stepper.AddChild(new Label { Text = _flaskCount.ToString(), FontOverride = _title, MinWidth = 36, Align = Label.AlignMode.Center, VerticalAlignment = VAlignment.Center });
        stepper.AddChild(Step("+", _flaskCount < max, () => _flaskCount++));
        stepper.AddChild(Step("MAX", _flaskCount < max, () => _flaskCount = max));
        body.AddChild(stepper);
        body.AddChild(new Control { VerticalExpand = true });

        var (label, enabled) = selected == null ? ("RACK EMPTY", false)
            : max <= 0 ? ("NOT ENOUGH IN THE JUG", false)
            : ($"FILL {_flaskCount} × {selected.Capacity}u", true);
        var button = ActionButton(label, enabled, true, 42);
        button.OnPressed += _ =>
        {
            if (selected == null)
                return;
            OnFillFlasks?.Invoke(selected.Capacity, _flaskCount);
            Log($"{_flaskCount} × {selected.Capacity}u flask");
        };
        body.AddChild(button);
        return card;
    }

    private Control BottleCard()
    {
        var (card, body) = Card("Bottles", "FOR DOCTORS", false);
        var count = Math.Min(MaxBottles, (int) Math.Ceiling(JugVolume.Float() / BottleVolume));
        body.AddChild(new Label { Text = $"{BottleVolume}u each · uses the label above", FontColorOverride = FSUiPalette.TextMuted });
        body.AddChild(new Control { VerticalExpand = true });
        var button = ActionButton(count <= 0 ? "JUG EMPTY" : $"BOTTLE {count}", count > 0, false, 36);
        button.OnPressed += _ =>
        {
            OnBottle?.Invoke(count);
            Log($"{count} bottle{(count == 1 ? "" : "s")}");
        };
        body.AddChild(button);
        return card;
    }

    private Control PillCard()
    {
        var (card, body) = Card("Pills", "FOR DOCTORS", false);
        var doses = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        foreach (var dose in Doses)
        {
            doses.AddChild(Option($"{dose}u", "", _dose == dose, true, () =>
            {
                _dose = dose;
                RefreshPackage();
            }, 56));
        }
        body.AddChild(doses);
        body.AddChild(new Control { VerticalExpand = true });

        var count = Math.Min(MaxPills, (int) (JugVolume.Float() / _dose));
        var button = ActionButton(count <= 0 ? "NOT ENOUGH IN THE JUG" : $"PRESS {count} × {_dose}u", count > 0, false, 36);
        button.OnPressed += _ =>
        {
            OnPill?.Invoke(_dose, count);
            Log($"{count} × {_dose}u pill{(count == 1 ? "" : "s")}");
        };
        body.AddChild(button);
        return card;
    }

    private Button Step(string text, bool enabled, Action change)
    {
        var button = ActionButton(text, enabled, false, 32);
        button.MinWidth = text.Length > 1 ? 48 : 32;
        button.OnPressed += _ =>
        {
            change();
            RefreshPackage();
        };
        return button;
    }
}
