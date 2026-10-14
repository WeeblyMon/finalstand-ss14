using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Perks;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Perks.UI;

public sealed partial class PerkShopWindow
{
    private static readonly Color RespecFill = Color.FromHex("#b3402f");

    private void RefreshInfo()
    {
        if (_selectedId == null || !FSPerkDef.All.TryGetValue(_selectedId, out var def))
        {
            SelectedPerkIcon.Texture = null;
            SelectedIconBox.PanelOverride = Box(FSUiPalette.BgRecess, FSUiPalette.BgTrack, new Thickness(1));
            CategoryTag.Visible = false;
            SelectedNameLabel.Text = "Select a perk";
            SelectedLevelLabel.Text = "";
            SelectedDescLabel.SetMessage(FormattedMessage.FromMarkupPermissive(
                $"[color={FSUiPalette.TextMuted.ToHex()}]Pick a perk from the list to see what each level does.[/color]"));
            LevelEffectsBox.RemoveAllChildren();
            ShortfallLabel.Visible = false;
            UpgradeButton.Visible = false;
            EquipButton.Visible = false;
            return;
        }

        var level = Level(def.Id);
        var locked = level == 0;
        var maxed = level == FSPerkDef.MaxLevel;
        var accent = FSPerkPalette.Accent[def.Category];

        SelectedPerkIcon.Texture = GetPerkIcon(def.Id);
        SelectedIconBox.PanelOverride = Box(FSPerkPalette.Background[def.Category], FSPerkPalette.Edge[def.Category], new Thickness(1));

        SelectedNameLabel.Text = def.Name;
        CategoryTag.Visible = true;
        CategoryTag.PanelOverride = Box(FSPerkPalette.Background[def.Category], FSPerkPalette.Edge[def.Category], new Thickness(1));
        SelectedCategoryLabel.Text = CatLabel[def.Category];
        SelectedCategoryLabel.FontColorOverride = accent;
        SelectedLevelLabel.Text = locked ? "Not owned" : $"Level {level} of {FSPerkDef.MaxLevel}";
        SelectedDescLabel.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={Color.FromHex("#c2beb8").ToHex()}]{FormattedMessage.EscapeText(def.Description)}[/color]"));

        LevelEffectsBox.RemoveAllChildren();
        for (var i = 0; i < FSPerkDef.MaxLevel; i++)
            LevelEffectsBox.AddChild(LadderRow(def, i, level, accent));

        var cost = FSPerkDef.CostForUpgrade(level);
        var points = _state?.PerkPoints ?? 0;
        var affordable = !maxed && points >= cost;

        UpgradeButton.Visible = true;
        UpgradeButton.Text = maxed ? "MAX LEVEL" : $"{(locked ? "UNLOCK" : $"UPGRADE TO L{level + 1}")} · {cost} PP";
        UpgradeButton.Disabled = !affordable;
        UpgradeButton.StyleBoxOverride = affordable
            ? Box(FSUiPalette.Currency, Color.FromHex("#e3bd70"), new Thickness(1))
            : Box(FSUiPalette.BgElevated, FSUiPalette.BgTrack, new Thickness(1));
        UpgradeButton.Label.FontColorOverride = affordable ? FSUiPalette.BgDeep : FSUiPalette.TextMuted;
        UpgradeButton.Label.HorizontalAlignment = HAlignment.Center;

        ShortfallLabel.Visible = !maxed && !affordable;
        ShortfallLabel.Text = $"You need {cost - points} more PP.";

        var slot = _state == null ? -1 : Array.IndexOf(_state.Slots, def.Id);
        var hasFree = _state?.Slots.Any(string.IsNullOrEmpty) == true;
        EquipButton.Visible = !locked;
        EquipButton.Disabled = slot < 0 && !hasFree;
        EquipButton.Text = slot >= 0 ? $"UNEQUIP FROM SLOT {slot + 1}" : hasFree ? "EQUIP TO NEXT FREE SLOT" : "ALL 6 SLOTS FULL";
        EquipButton.Label.HorizontalAlignment = HAlignment.Center;
    }

    private Control LadderRow(FSPerkDef def, int index, int level, Color accent)
    {
        var owned = index < level;
        var next = index == level;

        var row = new PanelContainer
        {
            PanelOverride = Box(owned ? Color.FromHex("#1d1b19") : Color.Transparent,
                next ? FSUiPalette.Currency : owned ? FSUiPalette.BgTrack : Color.Transparent,
                new Thickness(1), 10, 6),
        };

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        box.AddChild(new Label
        {
            Text = $"L{index + 1}",
            MinWidth = 22,
            FontOverride = _fontBodyBold,
            FontColorOverride = owned ? accent : FSUiPalette.BorderSubtle,
        });

        var text = new RichTextLabel { HorizontalExpand = true, MaxWidth = 190 };
        var color = owned || next ? FSUiPalette.TextPrimary : FSUiPalette.TextMuted;
        text.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color={color.ToHex()}]{FormattedMessage.EscapeText(def.LevelEffects[index])}[/color]"));
        box.AddChild(text);

        var tag = owned ? "OWNED" : next ? (level == 0 ? "UNLOCK" : "NEXT") : "";
        box.AddChild(new Label
        {
            Text = tag,
            FontOverride = _fontSmallBold,
            FontColorOverride = owned ? FSUiPalette.BorderSubtle : FSUiPalette.Currency,
            VerticalAlignment = VAlignment.Center,
        });

        row.AddChild(box);
        return row;
    }

    private void RefreshPrestigeTab()
    {
        var eligible = _playerLevel >= 50;

        PrestigeLevelLabel.Text = $"Prestige {_prestigeLevel} — Level {_playerLevel}";
        PrestigeXpBonusLabel.Text = $"Current XP Bonus: +{_prestigeLevel * 20}%";

        PrestigeRequirementLabel.Text = "Reach Level 50 to Prestige";
        PrestigeRequirementLabel.Modulate = eligible ? FSUiPalette.Currency : FSUiPalette.TextMuted;

        PrestigeButton.Disabled = !eligible;
        PrestigeButton.Modulate = eligible ? FSUiPalette.Currency : FSUiPalette.TextMuted;
    }

    // Shows exactly what a respec resets and what it keeps, and offers to bank the build first.
    private void ShowRespecConfirmation()
    {
        if (_state == null)
            return;

        var owned = _state.Levels.Where(kv => kv.Value > 0 && FSPerkDef.All.ContainsKey(kv.Key)).ToList();
        var equipped = _state.Slots.Count(s => !string.IsNullOrEmpty(s));
        var saved = _state.Loadouts.Count(l => l is { IsEmpty: false });
        var freeLoadout = Array.FindIndex(_state.Loadouts, l => l is not { IsEmpty: false });
        var refund = Invested(_state.Levels);

        var dialog = new FancyWindow { Title = "Respec", Resizable = false, SetWidth = 520 };
        ((Control)dialog).Stylesheet = ((Control)this).Stylesheet;

        var vbox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(20), SeparationOverride = 14 };

        vbox.AddChild(new Label { Text = "RESPEC ALL PERKS?", FontOverride = _fontTitle, FontColorOverride = FSUiPalette.TextPrimary });
        var intro = new RichTextLabel { MaxWidth = 480 };
        intro.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"You get [color={FSUiPalette.Currency.ToHex()}][bold]{refund} PP[/bold][/color] back and can rebuild from scratch."));
        vbox.AddChild(intro);

        var reset = Section("WILL BE RESET", FSPerkPalette.Accent[PerkCategory.Red],
            FSPerkPalette.Background[PerkCategory.Red], FSPerkPalette.Edge[PerkCategory.Red]);
        reset.Body.AddChild(BodyLabel($"{owned.Count} perks go back to level 0 · {equipped} equipped slots are cleared"));
        var chips = new GridContainer { Columns = 7, HSeparationOverride = 6, VSeparationOverride = 6 };
        foreach (var (id, lvl) in owned.OrderBy(kv => FSPerkDef.All[kv.Key].Category))
            chips.AddChild(OwnedChip(id, lvl));
        reset.Body.AddChild(chips);
        vbox.AddChild(reset.Root);

        var kept = Section("KEPT", FSPerkPalette.Accent[PerkCategory.Green],
            FSPerkPalette.Background[PerkCategory.Green], FSPerkPalette.Edge[PerkCategory.Green]);
        kept.Body.AddChild(BodyLabel(saved > 0
            ? $"Your {saved} saved loadout{(saved > 1 ? "s" : "")} — load one later to rebuild instantly"
            : "No saved loadouts yet"));
        kept.Body.AddChild(BodyLabel($"Your {_state.PerkPoints} unspent PP"));
        vbox.AddChild(kept.Root);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, HorizontalAlignment = HAlignment.Right };

        var cancel = DialogButton("CANCEL");
        cancel.OnPressed += _ => dialog.Close();
        buttons.AddChild(cancel);

        if (freeLoadout >= 0 && owned.Count > 0)
        {
            var bank = DialogButton($"SAVE TO LOADOUT {freeLoadout + 1} FIRST");
            bank.OnPressed += _ =>
            {
                OnSaveLoadout?.Invoke(new FSSaveLoadoutMessage { LoadoutIndex = freeLoadout });
                bank.Text = $"SAVED TO LOADOUT {freeLoadout + 1}";
                bank.Disabled = true;
            };
            buttons.AddChild(bank);
        }

        var confirm = DialogButton($"RESPEC · +{refund} PP");
        confirm.Disabled = owned.Count == 0;
        confirm.StyleBoxOverride = Box(RespecFill, FSPerkPalette.Accent[PerkCategory.Red], new Thickness(1), 16, 0);
        confirm.Label.FontColorOverride = Color.White;
        confirm.OnPressed += _ =>
        {
            OnRespecRequested?.Invoke();
            dialog.Close();
        };
        buttons.AddChild(confirm);

        vbox.AddChild(buttons);
        dialog.ContentsContainer.AddChild(vbox);
        dialog.OpenCentered();
        cancel.GrabKeyboardFocus();
    }

    private (PanelContainer Root, BoxContainer Body) Section(string title, Color accent, Color fill, Color edge)
    {
        var root = new PanelContainer { PanelOverride = Box(fill, edge, new Thickness(1), 14, 12) };
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        body.AddChild(new Label { Text = title, FontOverride = _fontSmallBold, FontColorOverride = accent });
        root.AddChild(body);
        return (root, body);
    }

    private Label BodyLabel(string text)
    {
        return new Label { Text = text, FontOverride = _fontBody, FontColorOverride = FSUiPalette.TextPrimary };
    }

    private Control OwnedChip(string id, int level)
    {
        var def = FSPerkDef.All[id];
        var chip = new PanelContainer
        {
            ToolTip = $"{def.Name} — level {level}",
            PanelOverride = Box(FSPerkPalette.Background[def.Category], FSPerkPalette.Edge[def.Category], new Thickness(1), 4, 2),
        };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        if (GetPerkIcon(id) is { } icon)
            row.AddChild(new TextureRect { Texture = icon, Stretch = TextureRect.StretchMode.Scale, SetSize = new Vector2(20, 20) });
        row.AddChild(new Label { Text = $"L{level}", FontOverride = _fontSmallBold, FontColorOverride = FSUiPalette.TextPrimary, VerticalAlignment = VAlignment.Center });
        chip.AddChild(row);
        return chip;
    }

    private Button DialogButton(string text)
    {
        var button = new Button { Text = text, MinHeight = 38 };
        button.Label.FontOverride = _fontSmallBold;
        button.Label.HorizontalAlignment = HAlignment.Center;
        return button;
    }

    private void ShowPrestigeConfirmation()
    {
        var dialog = new FancyWindow { Title = "Confirm Prestige", Resizable = false, SetSize = new Vector2(400, 200) };
        ((Control)dialog).Stylesheet = ((Control)this).Stylesheet;

        var vbox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(12) };
        vbox.AddChild(new Label { Text = "Prestige? Your level resets to 1.", Margin = new Thickness(0, 0, 0, 4) });
        vbox.AddChild(new Label { Text = "Perks and points are kept.", Margin = new Thickness(0, 0, 0, 4) });
        vbox.AddChild(new Label
        {
            Text = "You gain +20% permanent XP bonus.",
            FontColorOverride = FSUiPalette.Currency,
            Margin = new Thickness(0, 0, 0, 16),
        });

        var btnRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var confirm = new Button { Text = "Confirm", HorizontalExpand = true, Margin = new Thickness(0, 0, 6, 0) };
        confirm.Label.FontColorOverride = FSUiPalette.Currency;
        var cancel = new Button { Text = "Cancel", HorizontalExpand = true };

        confirm.OnPressed += _ =>
        {
            OnPrestigeRequested?.Invoke();
            dialog.Close();
        };
        cancel.OnPressed += _ => dialog.Close();

        btnRow.AddChild(confirm);
        btnRow.AddChild(cancel);
        vbox.AddChild(btnRow);
        dialog.ContentsContainer.AddChild(vbox);
        dialog.OpenCentered();
    }
}
