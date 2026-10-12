using System.Linq;
using Content.Client._FinalStand.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Perks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Perks.UI;

public sealed partial class PerkShopWindow
{
    private FancyWindow? _starterDialog;

    private void InitStarterBuilds()
    {
        StarterBanner.PanelOverride = Box(FSPerkPalette.Background[PerkCategory.Green], FSPerkPalette.Edge[PerkCategory.Green],
            new Thickness(0, 0, 0, 1));
        StarterBannerLabel.Text = Loc.GetString("fs-starter-build-banner");
        StarterBannerLabel.FontOverride = _fontBodyBold;
        StarterBannerLabel.FontColorOverride = FSPerkPalette.Accent[PerkCategory.Green];

        foreach (var button in new[] { StarterBannerButton, StarterBuildsButton })
        {
            button.Text = Loc.GetString("fs-starter-build-button");
            button.Label.FontOverride = _fontSmallBold;
            button.OnPressed += _ => ShowStarterBuilds();
        }
    }

    private void RefreshStarterBuilds()
    {
        StarterBanner.Visible = _state != null && _state.Levels.Values.All(level => level <= 0);

        if (_starterDialog is { Disposed: false, IsOpen: true })
            ShowStarterBuilds();
    }

    private void ShowStarterBuilds()
    {
        if (_state == null)
            return;

        if (_starterDialog is not { Disposed: false })
        {
            _starterDialog = new FancyWindow
            {
                Title = Loc.GetString("fs-starter-build-window-title"),
                Resizable = false,
                SetWidth = 620,
            };
            ((Control) _starterDialog).Stylesheet = ((Control) this).Stylesheet;
        }

        _starterDialog.ContentsContainer.RemoveAllChildren();

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(20),
            SeparationOverride = 12,
        };

        var hint = new RichTextLabel { MaxWidth = 580 };
        hint.SetMessage(Loc.GetString("fs-starter-build-window-hint"));
        vbox.AddChild(hint);

        var builds = IoCManager.Resolve<IPrototypeManager>()
            .EnumeratePrototypes<FSStarterBuildPrototype>()
            .OrderBy(b => b.Order);

        foreach (var build in builds)
            vbox.AddChild(StarterCard(build));

        _starterDialog.ContentsContainer.AddChild(vbox);
        if (!_starterDialog.IsOpen)
            _starterDialog.OpenCentered();
    }

    private Control StarterCard(FSStarterBuildPrototype build)
    {
        var plan = FSStarterBuildPlanner.Make(_state!.Levels, build.Perks, _state.PerkPoints);
        var known = build.Perks.Where(FSPerkDef.All.ContainsKey).ToList();

        var section = Section(Loc.GetString(build.Name).ToUpperInvariant(), FSPerkPalette.Accent[PerkCategory.Green],
            FSUiPalette.BgSurface, FSUiPalette.BorderNeutral);
        section.Body.AddChild(BodyLabel(Loc.GetString(build.Description)));

        var chips = new GridContainer { Columns = 6, HSeparationOverride = 6, VSeparationOverride = 6 };
        foreach (var perk in known)
            chips.AddChild(OwnedChip(perk, plan.Levels.GetValueOrDefault(perk)));
        section.Body.AddChild(chips);

        var footer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        var cost = new Label
        {
            Text = plan.Spent > 0
                ? Loc.GetString("fs-starter-build-cost", ("spent", plan.Spent), ("points", _state.PerkPoints))
                : Loc.GetString("fs-starter-build-nothing"),
            FontColorOverride = plan.Spent > 0 ? FSUiPalette.Currency : FSUiPalette.TextMuted,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        footer.AddChild(cost);

        var apply = DialogButton(Loc.GetString("fs-starter-build-apply"));
        apply.OnPressed += _ =>
        {
            OnApplyStarterBuild?.Invoke(new FSApplyStarterBuildMessage { BuildId = build.ID });
            _starterDialog?.Close();
        };
        footer.AddChild(apply);
        section.Body.AddChild(footer);

        return section.Root;
    }
}
