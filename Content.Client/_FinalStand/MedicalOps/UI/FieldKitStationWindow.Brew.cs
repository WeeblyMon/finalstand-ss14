using System.Linq;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.FixedPoint;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.MedicalOps.UI;

public sealed partial class FieldKitStationWindow
{
    private const int FillJug = -1;
    private const string DeliveryEither = "fs-field-kit-delivery-either";

    private static readonly (int Volume, string Title, string Sub)[] Batches =
    {
        (50, "FLASK", "50u"),
        (100, "100u", "2 flasks"),
        (200, "MAGAZINE", "200u"),
        (FillJug, "FILL JUG", ""),
    };

    private int _batch = 200;

    // Everything a card needs to know about brewing one kit at the chosen batch size.
    private sealed record BrewPlan(FSFieldKitPrototype Kit, ReactionPrototype Reaction, Color Color, int Volume,
        List<(string Reagent, FixedPoint2 Need, FixedPoint2 Have, bool FromTank)> Parts, string? Blocker, bool ResearchLocked);

    private void BuildBatchRow()
    {
        BatchRow.RemoveAllChildren();
        foreach (var (volume, title, sub) in Batches)
        {
            var label = volume == FillJug ? $"{FillVolume()}u" : sub;
            BatchRow.AddChild(Option(title, label, _batch == volume, true, () =>
            {
                _batch = volume;
                BuildBatchRow();
                RefreshCards();
            }, 82));
        }
    }

    private void RefreshBrewStrip()
    {
        var satchel = _satchel < 0 ? FixedPoint2.Zero : _satchel;
        SatchelLabel.Text = $"{satchel}u";

        var tank = _state?.Tank ?? FixedPoint2.Zero;
        var tankMax = _state?.TankMax ?? FixedPoint2.New(500);
        TankLabel.Text = $"{tank} / {tankMax}u";
        TankBar.RemoveAllChildren();
        TankBar.AddChild(Segment(Olive, tankMax > 0 ? tank.Float() / tankMax.Float() : 0f));
        TankBar.AddChild(Segment(FSUiPalette.BgTrack, tankMax > 0 ? 1f - tank.Float() / tankMax.Float() : 1f));

        var room = tankMax - tank;
        var dump = FixedPoint2.Min(satchel, room);
        var canDump = dump > 0;
        DumpButton.Text = satchel <= 0 ? "SATCHEL EMPTY" : room <= 0 ? "TANK FULL" : $"DUMP {dump}u  →";
        DumpButton.Disabled = !canDump;
        DumpButton.StyleBoxOverride = canDump
            ? Box(Olive, OliveText, new Thickness(1), 14, 0)
            : Box(FSUiPalette.BgElevated, FSUiPalette.BgTrack, new Thickness(1), 14, 0);
        DumpButton.Label.FontColorOverride = canDump ? FSUiPalette.BgDeep : FSUiPalette.TextMuted;
        DumpButton.ToolTip = "Empties every harvest satchel you are carrying. Using a satchel on the station does the same.";
    }

    private int FillVolume()
    {
        var yield = 2f;
        return (int) (Math.Floor(JugFree.Float() / yield) * yield);
    }

    private void RefreshCards()
    {
        CardGrid.RemoveAllChildren();
        foreach (var plan in Plans())
            CardGrid.AddChild(BuildCard(plan));
    }

    private IEnumerable<BrewPlan> Plans()
    {
        var volume = _batch == FillJug ? FillVolume() : _batch;
        foreach (var kit in _prototype.EnumeratePrototypes<FSFieldKitPrototype>().OrderBy(k => k.Priority))
        {
            if (kit.Reaction is not { } reactionId || !_prototype.TryIndex(reactionId, out ReactionPrototype? reaction))
                continue;

            var yield = reaction.Products.Values.Aggregate(FixedPoint2.Zero, (sum, q) => sum + q);
            var product = reaction.Products.Keys.FirstOrDefault();
            var batches = yield > 0 ? (int) (volume / yield.Float()) : 0;
            var parts = new List<(string, FixedPoint2, FixedPoint2, bool)>();
            string? blocker = null;
            var locked = false;

            foreach (var (reagent, reactant) in reaction.Reactants)
            {
                var need = reactant.Amount * batches;
                var fromTank = reagent == TissueReagent;
                var have = fromTank ? _state?.Tank ?? FixedPoint2.Zero : Stocked(reagent);
                parts.Add((reagent, need, have, fromTank));

                if (have >= need)
                    continue;
                if (!fromTank && have <= 0 && kit.Research)
                {
                    locked = true;
                    blocker = $"NEEDS {ReagentName(reagent).ToUpperInvariant()}";
                }
                else
                {
                    blocker ??= $"{need - have}u {(fromTank ? "TISSUE" : ReagentName(reagent).ToUpperInvariant())} SHORT";
                }
            }

            if (_state?.Jug == null)
                blocker = "NO JUG";
            else if (batches < 1 || volume > JugFree.Float())
                blocker = "JUG FULL";
            else if (_state is { Powered: false })
                blocker = "NO POWER";

            yield return new BrewPlan(kit, reaction, product != null ? ReagentColor(product) : Color.White,
                (int) (yield.Float() * batches), parts, blocker, locked);
        }
    }

    private FixedPoint2 Stocked(string reagent)
    {
        var total = FixedPoint2.Zero;
        foreach (var item in _state?.Inventory ?? new())
        {
            if (item.ReagentId == reagent)
                total += item.Quantity;
        }
        return total;
    }

    private Control BuildCard(BrewPlan plan)
    {
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            MinHeight = 250,
            MouseFilter = MouseFilterMode.Pass,
            Modulate = plan.ResearchLocked ? Color.White.WithAlpha(0.6f) : Color.White,
            PanelOverride = Box(FSUiPalette.BgSurface, FSUiPalette.BgTrack, new Thickness(1)),
        };
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        column.AddChild(new PanelContainer { SetHeight = 4, PanelOverride = new StyleBoxFlat { BackgroundColor = plan.Color } });

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(14, 12, 14, 14), VerticalExpand = true };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        head.AddChild(Swatch(plan.Color, 12, 12));
        head.AddChild(new Label { Text = Loc.GetString(plan.Kit.Name), FontOverride = _title, FontColorOverride = FSUiPalette.TextPrimary, HorizontalExpand = true, ClipText = true });
        foreach (var tag in plan.Kit.Delivery == DeliveryEither ? new[] { "DART", "FLASK" } : new[] { "FLASK" })
        {
            var chip = new PanelContainer { VerticalAlignment = VAlignment.Center, PanelOverride = Box(Color.Transparent, FSUiPalette.BorderNeutral, new Thickness(1), 4, 0) };
            chip.AddChild(new Label { Text = tag, FontOverride = _caption, FontColorOverride = FSUiPalette.TextMuted });
            head.AddChild(chip);
        }
        body.AddChild(head);
        body.AddChild(Wrapped(Loc.GetString(plan.Kit.Purpose), Color.FromHex("#c2beb8"), 220));
        body.AddChild(new Control { VerticalExpand = true });

        foreach (var (reagent, need, have, fromTank) in plan.Parts)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            row.AddChild(Swatch(fromTank ? Tissue : ReagentColor(reagent), 8, 8));
            row.AddChild(new Label { Text = $"{(fromTank ? "Tissue" : ReagentName(reagent))} {need}u", HorizontalExpand = true, ClipText = true });
            var ok = have >= need;
            row.AddChild(new Label
            {
                Text = fromTank ? $"tank {have}u" : have > 0 ? $"stock {have}u" : plan.ResearchLocked ? "research" : "none",
                FontOverride = _caption,
                FontColorOverride = ok ? OliveText : plan.ResearchLocked ? Research : FSUiPalette.StateNegative,
            });
            body.AddChild(row);
        }

        var flasks = plan.Volume / 50;
        body.AddChild(new Label
        {
            Text = $"→ {plan.Volume}u" + (plan.Volume >= 200 ? " · 1 magazine" : "") + $" · {flasks} flask{(flasks == 1 ? "" : "s")}",
            FontOverride = _caption,
            FontColorOverride = FSUiPalette.TextMuted,
        });
        if (plan.ResearchLocked)
            body.AddChild(Wrapped("Research unlock: print the bottle at the lathe, then load it here.", Research, 220));

        var brew = ActionButton(plan.Blocker ?? $"BREW {plan.Volume}u", plan.Blocker == null, true);
        brew.OnPressed += _ =>
        {
            OnBrew?.Invoke(plan.Kit.ID, plan.Volume);
            _preview = null;
        };
        body.AddChild(brew);

        column.AddChild(body);
        card.AddChild(column);

        card.OnMouseEntered += _ =>
        {
            if (plan.Blocker != null)
                return;
            _preview = (plan.Color, plan.Volume);
            RefreshSidebar();
        };
        card.OnMouseExited += _ =>
        {
            _preview = null;
            RefreshSidebar();
        };
        return card;
    }
}
