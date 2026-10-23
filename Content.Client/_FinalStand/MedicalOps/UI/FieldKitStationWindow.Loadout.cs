using System.Linq;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;

namespace Content.Client._FinalStand.MedicalOps.UI;

public sealed partial class FieldKitStationWindow
{
    private int _targetSlot;
    private string _launcherKey = string.Empty;

    // The launcher in the local player's hands, or failing that the first one in their inventory.
    private Entity<FSChemLauncherComponent>? LocalLauncher()
    {
        if (_player.LocalEntity is not { } player)
            return null;

        var hands = _entity.System<SharedHandsSystem>();
        if (hands.TryGetActiveItem(player, out var held) && _entity.TryGetComponent<FSChemLauncherComponent>(held, out var heldLauncher))
            return (held.Value, heldLauncher);

        return Find(player, 0);

        Entity<FSChemLauncherComponent>? Find(EntityUid root, int depth)
        {
            if (!_entity.TryGetComponent<ContainerManagerComponent>(root, out var manager))
                return null;

            foreach (var container in manager.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (_entity.TryGetComponent<FSChemLauncherComponent>(item, out var launcher))
                        return (item, launcher);

                    if (depth < 1 && Find(item, depth + 1) is { } nested)
                        return nested;
                }
            }

            return null;
        }
    }

    private string LauncherKey()
    {
        if (LocalLauncher() is not { } launcher)
            return "none";

        var c = launcher.Comp;
        return $"{launcher.Owner}|{string.Join(',', c.Slots)}|{c.SlotCount}|{string.Join(',', c.Known)}|{(int) c.Reserve}|{(int) c.MaxReserve}|{(int) c.Catalyst}|{c.CatalystArmed}";
    }

    private void RefreshLoadout()
    {
        var launcher = LocalLauncher();
        var comp = launcher?.Comp;

        LauncherLabel.Text = launcher is { } held ? _entity.GetComponent<MetaDataComponent>(held).EntityName : "Not carried";
        LauncherLabel.FontColorOverride = launcher == null ? Dim : FSUiPalette.TextPrimary;

        ReserveBar.RemoveAllChildren();
        var ratio = comp is { MaxReserve: > 0f } ? Math.Clamp(comp.Reserve / comp.MaxReserve, 0f, 1f) : 0f;
        ReserveBar.AddChild(Segment(Olive, ratio));
        ReserveBar.AddChild(Segment(FSUiPalette.BgTrack, 1f - ratio));
        ReserveHint.Text = comp == null
            ? "Carry your launcher to set its formulas."
            : $"{comp.Reserve:0} / {comp.MaxReserve:0} reserve · catalyst {(comp.CatalystArmed ? "armed" : $"{comp.Catalyst:0}%")}";

        var slots = comp?.SlotCount ?? 3;
        _targetSlot = Math.Clamp(_targetSlot, 0, Math.Max(0, slots - 1));

        SlotRow.RemoveAllChildren();
        for (var i = 0; i < slots; i++)
        {
            var index = i;
            var name = comp != null && i < comp.Slots.Count && _prototype.TryIndex(comp.Slots[i], out var formula)
                ? Loc.GetString(formula.Name).ToUpperInvariant()
                : "EMPTY";
            SlotRow.AddChild(Option(name, $"SLOT {i + 1}", i == _targetSlot, comp != null, () =>
            {
                _targetSlot = index;
                RefreshLoadout();
            }, 120));
        }

        var prep = _state?.Prep ?? false;
        TonicButton.Text = prep ? "BREW TONIC" : "PREP ONLY";
        TonicButton.Disabled = !prep;
        StyleAction(TonicButton, prep, true);
        TonicHint.Text = "Each crewmate drinks once. Lasts the wave.";

        FormulaGrid.RemoveAllChildren();
        foreach (var formula in _prototype.EnumeratePrototypes<FSChemFormulaPrototype>().OrderBy(f => f.Priority))
            FormulaGrid.AddChild(FormulaCard(formula, comp));
    }

    private Control FormulaCard(FSChemFormulaPrototype formula, FSChemLauncherComponent? launcher)
    {
        var known = launcher?.Known.Contains(formula.ID) ?? formula.Research == null;
        var slot = launcher?.Slots.IndexOf(formula.ID) ?? -1;
        var colour = _prototype.TryIndex<ReagentPrototype>(formula.Reagent, out var reagent) ? reagent.SubstanceColor.WithAlpha(1f) : Color.White;
        var hostile = formula.Kind == FSChemFormulaKind.Hostile;

        var card = new PanelContainer
        {
            HorizontalExpand = true,
            MinHeight = 190,
            Modulate = known ? Color.White : Color.White.WithAlpha(0.6f),
            PanelOverride = Box(FSUiPalette.BgSurface, slot >= 0 ? FSUiPalette.Currency : FSUiPalette.BgTrack, new Thickness(1)),
        };
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        column.AddChild(new PanelContainer { SetHeight = 4, PanelOverride = new StyleBoxFlat { BackgroundColor = colour } });

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(14, 12, 14, 14), VerticalExpand = true };
        var head = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        head.AddChild(Swatch(colour, 12, 12));
        head.AddChild(new Label { Text = Loc.GetString(formula.Name), FontOverride = _title, FontColorOverride = FSUiPalette.TextPrimary, HorizontalExpand = true, ClipText = true });
        var chip = new PanelContainer { VerticalAlignment = VAlignment.Center, PanelOverride = Box(Color.Transparent, FSUiPalette.BorderNeutral, new Thickness(1), 4, 0) };
        chip.AddChild(new Label { Text = hostile ? "HOSTILE" : "SUPPORT", FontOverride = _caption, FontColorOverride = hostile ? FSUiPalette.StateNegative : OliveText });
        head.AddChild(chip);
        body.AddChild(head);
        body.AddChild(Wrapped(Loc.GetString(formula.Purpose), Body, 300));
        body.AddChild(new Control { VerticalExpand = true });
        body.AddChild(new Label
        {
            Text = slot >= 0 ? $"{formula.Cost} reserve a shot · in slot {slot + 1}" : $"{formula.Cost} reserve a shot",
            FontOverride = _caption,
            FontColorOverride = FSUiPalette.TextMuted,
        });

        string text;
        var enabled = false;
        if (!known)
            text = formula.Research is { } node && _prototype.TryIndex<FSTechNodePrototype>(node, out var tech)
                ? $"RESEARCH: {tech.Name.ToUpperInvariant()}"
                : "LOCKED";
        else if (launcher == null)
            text = "CARRY YOUR LAUNCHER";
        else if (slot == _targetSlot)
            text = $"IN SLOT {slot + 1}";
        else
        {
            enabled = true;
            text = slot >= 0 ? $"MOVE TO SLOT {_targetSlot + 1}" : $"LOAD INTO SLOT {_targetSlot + 1}";
        }

        var button = ActionButton(text, enabled, true, 36);
        if (!known)
            button.Label.FontColorOverride = Research;
        var target = _targetSlot;
        button.OnPressed += _ => OnSetSlot?.Invoke(target, formula.ID);
        body.AddChild(button);

        column.AddChild(body);
        card.AddChild(column);
        return card;
    }
}
