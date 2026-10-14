using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Research.Systems;
using Content.Shared.Materials;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Research.UI;

public sealed partial class FSResearchTreeMenu
{
    // Stats where a bigger number is worse, so "+6° Spread" reads as a drawback.
    private static readonly string[] InverseStats = { "Spread", "Cooldown", "Recoil", "Heat" };
    private static readonly Color DrawbackText = Color.FromHex("#e0a598");
    private static readonly Color ShopAmber = Color.FromHex("#c08a3e");
    private static readonly Color DoneOlive = Color.FromHex("#7e9464");

    private void UpdateDetailPanel()
    {
        GraphControl.SelectedId = _selectedNode?.Id;
        UpdateGraphHeader();
        RebuildQueueList();

        if (!_warningIsSticky)
            WarnLabel.Visible = false;

        var node = _selectedNode;
        EmptyStateLabel.Visible = node == null;
        DetailBody.Visible = node != null;
        ClearButton.Visible = false;
        QueueButton.Visible = false;

        if (node == null)
        {
            SetPrimary("SELECT A TECHNOLOGY", false);
            return;
        }

        var done = node.IsDone;
        var blocked = node.State == FSResearchNodeState.ExclusivelyBlocked;
        var available = node.State == FSResearchNodeState.Available;
        var medical = IsMedicalTrack();

        UpdatePlate(node);
        BuildEffects(node);
        BuildRequirements(node);
        var hasMaterials = BuildCost(node, medical);

        ProgressSection.Visible = !medical;
        ProgressBar.RemoveAllChildren();
        ProgressBar.AddChild(Bar(done ? 1f : node.ProgressFraction, done ? DoneOlive : node.IsActiveResearch ? Gold : OffWhite, 6));
        ProgressLabel.Text = done ? "Complete" : string.Format(CultureInfo.InvariantCulture, "{0:N0} / {1:N0} RP", node.Progress, node.Cost);

        if (medical)
        {
            UpdateMedicalActions(node);
            return;
        }

        var others = node.PersonalContributorCount - (node.IsMyPersonalPick ? 1 : 0);
        var contribution = done ? ""
            : node.IsActiveResearch ? "Everyone without a pick funds this at 2× speed."
            : node.IsMyPersonalPick ? (others > 0 ? $"You and {others} other scientist{(others > 1 ? "s are" : " is")} funding this." : "You are funding this.")
            : others > 0 ? $"{others} scientist{(others > 1 ? "s are" : " is")} researching this."
            : available ? "No one is researching this yet." : "";
        ContribLabel.Visible = contribution.Length > 0;
        ContribLabel.SetMessage(Markup(contribution, FSUiPalette.TextMuted));

        var rd = _fsResearch.IsRdOrCaptain;
        if (done)
            SetPrimary("RESEARCHED", false);
        else if (blocked)
            SetPrimary("LOCKED OUT", false);
        else if (!available)
            SetPrimary("LOCKED — FINISH REQUIREMENTS", false);
        else if (node.IsMyPersonalPick && !rd)
        {
            SetPrimary("RESEARCHING — YOUR PICK", false);
            ShowClear("STOP MY PICK");
        }
        else if (node.IsActiveResearch && rd)
        {
            SetPrimary("CURRENT SHARED TARGET", false);
            ShowClear("CLEAR SHARED TARGET");
        }
        else if (!hasMaterials)
            SetPrimary("NEED MORE MATERIALS", false);
        else if (rd)
            SetPrimary("SET AS SHARED TARGET", true);
        else if (node.IsActiveResearch)
            SetPrimary("ALSO PICK THIS (FULL COST)", true);
        else
            SetPrimary(others > 0 ? $"JOIN RESEARCH ({others} OTHER{(others > 1 ? "S" : "")})" : "RESEARCH THIS", true);

        if (available && !hasMaterials)
            ShowWarning("Not enough materials in the console. Load more to start.");

        UpdateQueueButton(node);
    }

    private void UpdatePlate(FSResearchNodeView node)
    {
        var (stateText, stateColor) = node.IsDone ? ("RESEARCHED", Olive)
            : node.State == FSResearchNodeState.ExclusivelyBlocked ? ("LOCKED OUT", Rust)
            : node.IsActiveResearch ? ("RD TARGET", Gold)
            : node.IsMyPersonalPick ? ("YOUR PICK", OffWhite)
            : node.State == FSResearchNodeState.Available ? ("AVAILABLE", OffWhite)
            : ("LOCKED", FSUiPalette.TextMuted);

        DetailName.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[font size=14][bold][color={OffWhite.ToHex()}]{FormattedMessage.EscapeText(node.Name)}[/color][/bold][/font]"));
        StateChipLabel.Text = stateText;
        StateChipLabel.FontColorOverride = stateColor;
        StateChip.PanelOverride = Box(stateColor.WithAlpha(0.12f), stateColor.WithAlpha(0.45f), new Thickness(1));
        TierChipLabel.Text = "TIER " + node.Tier;
        TierChipLabel.FontColorOverride = FSUiPalette.TextMuted;
        TierChip.PanelOverride = Box(Color.Transparent, FSUiPalette.BorderNeutral, new Thickness(1));

        var lit = node.IsDone || node.IsActive || node.State == FSResearchNodeState.Available;
        var ring = node.IsCapstone && !node.IsDone ? ShopAmber : node.IsDone ? DoneOlive : stateColor;
        DetailIcon.Sprite = node.Proto.Icon;
        DetailIcon.Tint = Color.White.WithAlpha(lit ? 1f : 0.45f);
        DetailDiscRing.Modulate = ring;
        DetailDiscGlow.Modulate = ring.WithAlpha(lit ? 0.35f : 0f);
    }

    private void UpdateMedicalActions(FSResearchNodeView node)
    {
        if (node.IsDone)
        {
            SetPrimary(Loc.GetString("fs-medical-research-owned").ToUpperInvariant(), false);
            return;
        }

        var available = node.State == FSResearchNodeState.Available;
        var affordable = (Database?.Points ?? 0) >= node.Cost;
        SetPrimary(Loc.GetString("fs-medical-research-purchase").ToUpperInvariant(), available && affordable);
        if (available && !affordable)
            ShowWarning(Loc.GetString("fs-medical-research-cannot-afford"));
    }

    private void SetPrimary(string text, bool enabled)
    {
        PrimaryButton.Text = text;
        PrimaryButton.Disabled = !enabled;
        PrimaryButton.StyleBoxOverride = enabled
            ? Box(Gold, Color.FromHex("#e3bd70"), new Thickness(1))
            : Box(FSUiPalette.BgElevated, FSUiPalette.BgTrack, new Thickness(1));
        PrimaryButton.Label.FontColorOverride = enabled ? FSUiPalette.BgDeep : FSUiPalette.TextMuted;
    }

    private void ShowClear(string text)
    {
        ClearButton.Visible = true;
        ClearButton.Text = text;
    }

    private void BuildEffects(FSResearchNodeView node)
    {
        EffectList.RemoveAllChildren();
        var proto = node.Proto;

        if (proto.VanillaTechnologyId is { } vanillaId && _prototype.TryIndex(vanillaId, out var vanilla))
        {
            var desc = new RichTextLabel { MaxWidth = 296 };
            desc.SetMessage(_research.GetTechnologyDescription(vanilla, includeCost: false, includeTier: false));
            EffectList.AddChild(desc);
        }

        var bonus = proto.BonusDescription.Length == 0 ? ""
            : Loc.TryGetString(proto.BonusDescription, out var localised) ? localised : proto.BonusDescription;
        var lines = bonus.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        if (proto.WeaponShopUnlock is { } shop && !lines.Any(l => l.Contains("shop", StringComparison.OrdinalIgnoreCase)))
            lines.Insert(0, $"Unlocks the {(_prototype.TryIndex<EntityPrototype>(shop, out var shopProto) ? shopProto.Name : shop.Id)} shop");

        if (lines.Count == 0 && EffectList.ChildCount == 0)
            lines.Add("No listed effect.");

        foreach (var line in lines)
        {
            var bad = IsDrawback(line);
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            row.AddChild(new Label { Text = bad ? "▼" : "▲", FontOverride = _captionFont, FontColorOverride = bad ? Rust : Olive, VerticalAlignment = VAlignment.Top, Margin = new Thickness(0, 3, 0, 0) });
            var text = new RichTextLabel { MaxWidth = 276, HorizontalExpand = true };
            text.SetMessage(Markup(line, bad ? DrawbackText : OffWhite));
            row.AddChild(text);
            EffectList.AddChild(row);
        }

        var group = proto.ExclusiveGroup;
        ExclusiveNote.Visible = group != null && !node.IsDone;
        if (group == null)
            return;

        var names = string.Join(" or ", _prototype.EnumeratePrototypes<FSTechNodePrototype>()
            .Where(p => p.ExclusiveGroup == group && p.ID != node.Id)
            .Select(p => p.Name));
        ExclusiveNoteLabel.SetMessage(Markup(node.State == FSResearchNodeState.ExclusivelyBlocked
            ? $"Locked out: {names} was researched instead."
            : $"Choose one: researching this permanently locks out {names}.", OffWhite));
    }

    private static bool IsDrawback(string line)
    {
        if (line.Length == 0 || (line[0] != '+' && line[0] != '-'))
            return false;
        var inverse = InverseStats.Any(s => line.Contains(s, StringComparison.OrdinalIgnoreCase));
        return (line[0] == '-') != inverse;
    }

    private void BuildRequirements(FSResearchNodeView node)
    {
        RequiresList.RemoveAllChildren();
        var proto = node.Proto;
        if (proto.Prerequisites.Count == 0 && proto.PrerequisiteGroups.Count == 0)
        {
            RequiresList.AddChild(CheckRow(true, "Nothing: a starting technology"));
            return;
        }

        foreach (var id in proto.Prerequisites)
            RequiresList.AddChild(CheckRow(IsIdUnlocked(id), ResolveNodeName(id)));
        foreach (var group in proto.PrerequisiteGroups)
            RequiresList.AddChild(CheckRow(group.Any(IsIdUnlocked), string.Join(" or ", group.Select(ResolveNodeName)) + "  (either one)"));
    }

    private BoxContainer CheckRow(bool ok, string text)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new Label { Text = ok ? "✓" : "·", MinWidth = 12, FontOverride = _boldFont, FontColorOverride = ok ? Olive : Dim, VerticalAlignment = VAlignment.Top });
        var label = new RichTextLabel { MaxWidth = 276, HorizontalExpand = true };
        label.SetMessage(Markup(text, ok ? FSUiPalette.TextMuted : OffWhite));
        row.AddChild(label);
        return row;
    }

    // Returns whether the console holds every material the node needs.
    private bool BuildCost(FSResearchNodeView node, bool medical)
    {
        CostGrid.RemoveAllChildren();
        foreach (var header in new[] { "MATERIAL", "NEED", "IN STOCK" })
        {
            var first = header == "MATERIAL";
            CostGrid.AddChild(new Label { Text = header, FontOverride = _captionFont, FontColorOverride = Dim, HorizontalExpand = first, Align = first ? Label.AlignMode.Left : Label.AlignMode.Right, MinWidth = first ? 0 : 64 });
        }

        if (medical)
        {
            var funds = Database?.Points ?? 0;
            AddCostRow("Funds", Money(node.Cost), Money(funds), funds >= node.Cost ? Olive : Rust);
        }
        else
        {
            AddCostRow("Research", string.Format(CultureInfo.InvariantCulture, "{0:N0} RP", node.Cost), "", FSUiPalette.TextMuted);
        }

        var materials = node.Proto.MaterialCost;
        if (materials.Count == 0)
            return true;

        var stored = _entity.HasComponent<MaterialStorageComponent>(_console)
            ? _materialStorage.GetStoredMaterials(_console)
            : new Dictionary<ProtoId<MaterialPrototype>, int>();
        var enough = true;
        foreach (var (matId, amount) in materials)
        {
            var name = _prototype.TryIndex(matId, out var mat) ? Loc.GetString(mat.Name) : matId.Id;
            if (name.Length > 0)
                name = name.Substring(0, 1).ToUpperInvariant() + name.Substring(1);
            var have = stored.GetValueOrDefault(matId);
            enough &= have >= amount;
            AddCostRow(name, amount.ToString("N0", CultureInfo.InvariantCulture), have.ToString("N0", CultureInfo.InvariantCulture), have >= amount ? Olive : Rust);
        }

        return enough;
    }

    private static string Money(int amount) => string.Format(CultureInfo.InvariantCulture, "${0:N0}", amount);

    private void AddCostRow(string name, string need, string have, Color haveColor)
    {
        CostGrid.AddChild(new Label { Text = name, FontColorOverride = OffWhite, HorizontalExpand = true, ClipText = true });
        CostGrid.AddChild(new Label { Text = need, FontOverride = _boldFont, FontColorOverride = OffWhite, Align = Label.AlignMode.Right });
        CostGrid.AddChild(new Label { Text = have, FontOverride = _boldFont, FontColorOverride = haveColor, Align = Label.AlignMode.Right });
    }

    private List<string> MyQueue()
        => _fsResearch.IsRdOrCaptain
            ? Database?.SharedQueue.Select(n => n.Id).ToList() ?? new List<string>()
            : _fsResearch.MyPersonalQueue;

    private void UpdateQueueButton(FSResearchNodeView node)
    {
        if (node.IsDone || node.State == FSResearchNodeState.ExclusivelyBlocked)
            return;
        if (_fsResearch.IsRdOrCaptain ? node.IsActiveResearch : node.IsMyPersonalPick)
            return;

        var queue = MyQueue();
        var queued = queue.IndexOf(node.Id);
        var full = queue.Count >= SharedFSResearchSystem.MaxQueueLength;
        QueueButton.Visible = true;
        QueueButton.Text = queued >= 0 ? $"QUEUED #{queued + 1}" : full ? "QUEUE FULL" : "ADD TO QUEUE";
        QueueButton.Disabled = queued >= 0 || full;
    }

    private void RebuildQueueList()
    {
        QueueList.RemoveAllChildren();
        var medical = IsMedicalTrack();
        QueueTitle.Visible = !medical;
        QueueList.Visible = !medical;
        if (medical)
            return;

        var queue = MyQueue();
        QueueTitle.Text = $"{(_fsResearch.IsRdOrCaptain ? "SHARED QUEUE" : "MY QUEUE")} {queue.Count} / {SharedFSResearchSystem.MaxQueueLength}";

        if (queue.Count == 0)
        {
            var empty = new RichTextLabel { MaxWidth = 296 };
            empty.SetMessage(Markup("Nothing queued. Queued tech starts when the current one finishes.", Dim));
            QueueList.AddChild(empty);
            return;
        }

        for (var i = 0; i < queue.Count; i++)
        {
            var id = queue[i];
            if (!_prototype.TryIndex<FSTechNodePrototype>(id, out var proto))
                continue;

            var row = new PanelContainer { MinHeight = 28, PanelOverride = Box(FSUiPalette.BgDeep, FSUiPalette.BgTrack, new Thickness(1), 8, 0) };
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            line.AddChild(new Label { Text = (i + 1).ToString(CultureInfo.InvariantCulture), FontOverride = _captionFont, FontColorOverride = Dim, VerticalAlignment = VAlignment.Center });
            line.AddChild(new FSCroppedIcon { Sprite = proto.Icon, SetSize = new Vector2(18, 18), VerticalAlignment = VAlignment.Center });
            var nameButton = new Button { Text = proto.Name, HorizontalExpand = true, StyleBoxOverride = new StyleBoxEmpty() };
            nameButton.Label.ClipText = true;
            nameButton.OnPressed += _ => SelectById(id);
            line.AddChild(nameButton);
            var remove = new Button { Text = "✕", MinWidth = 24, StyleBoxOverride = new StyleBoxEmpty(), ToolTip = "Remove from queue" };
            remove.Label.FontColorOverride = FSUiPalette.TextMuted;
            remove.OnPressed += _ => OnFsNodeDequeued?.Invoke(id);
            line.AddChild(remove);
            row.AddChild(line);
            QueueList.AddChild(row);
        }
    }

    private bool IsIdUnlocked(string id)
    {
        if (_entity.TryGetComponent<TechnologyDatabaseComponent>(_console, out var db) && db.UnlockedTechnologies.Any(u => u.Id == id))
            return true;
        return Database?.UnlockedNodes.Any(u => u.Id == id) == true;
    }

    private string ResolveNodeName(string id)
    {
        if (_prototype.TryIndex<FSTechNodePrototype>(id, out var fsProto))
            return fsProto.Name;
        if (_prototype.TryIndex<TechnologyPrototype>(id, out var techProto))
            return Loc.GetString(techProto.Name);
        return id;
    }
}
