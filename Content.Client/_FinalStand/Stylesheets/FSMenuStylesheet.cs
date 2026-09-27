using System.Collections.Generic;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._FinalStand.Stylesheets;

// Shared stylesheet for every FS menu window, sourced from FSUiPalette.
public sealed class FSMenuStylesheet
{
    private static Stylesheet? _cached;

    public Stylesheet Stylesheet { get; }

    // Built once. The rules hold colours only, so every FS window can share one instance.
    public static Stylesheet Get(IUserInterfaceManager uiManager, IResourceCache resCache)
        => _cached ??= new FSMenuStylesheet(uiManager).Stylesheet;

    // Square with a 1px edge, like the HUD's panels.
    public static StyleBoxFlat CardPanel(Color fill)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = fill,
            BorderColor = FSUiPalette.BgTrack,
            BorderThickness = new Thickness(1),
        };
    }

    public FSMenuStylesheet(IUserInterfaceManager uiManager)
    {
        StyleBoxFlat Box(Color bg, Color? border = null, float bw = 0, float ph = 8, float pv = 4) =>
            new StyleBoxFlat
            {
                BackgroundColor             = bg,
                BorderColor                 = border ?? bg,
                BorderThickness             = new Thickness(bw),
                ContentMarginLeftOverride   = ph,
                ContentMarginRightOverride  = ph,
                ContentMarginTopOverride    = pv,
                ContentMarginBottomOverride = pv,
            };

        var winPanel   = CardPanel(FSUiPalette.BgDeep);
        var cardPanel  = CardPanel(FSUiPalette.BgSurface);
        var winHeader  = Box(FSUiPalette.BgRecess);
        var btnNormal  = Box(FSUiPalette.BgDeep, FSUiPalette.BorderNeutral, 1);
        var btnHover   = Box(FSUiPalette.BgElevated, FSUiPalette.BorderSubtle, 1);
        var btnPressed = Box(FSUiPalette.BgPressed, FSUiPalette.BorderPressed, 1);
        var btnDisable = Box(FSUiPalette.BgDisabled, FSUiPalette.BorderDisabled, 1);

        var divider = new StyleBoxFlat
        {
            BackgroundColor             = FSUiPalette.BorderNeutral,
            ContentMarginTopOverride    = 1,
            ContentMarginBottomOverride = 1,
        };

        var scrollGrab      = Box(FSUiPalette.BorderNeutral, null, 0, 3, 3);
        var scrollGrabHover = Box(FSUiPalette.BorderSubtle,  null, 0, 3, 3);
        var scrollGrabGrab  = Box(FSUiPalette.AccentBrand,   null, 0, 3, 3);

        var custom = new List<StyleRule>
        {
            // DefaultWindow and FancyWindow use different style classes for the same chrome roles.
            Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, winPanel),
            Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowHeader)
                .Prop(PanelContainer.StylePropertyPanel, winHeader),
            Element<Label>().Class(DefaultWindow.StyleClassWindowTitle)
                .Prop(Label.StylePropertyFontColor, FSUiPalette.TextPrimary),

            Element<PanelContainer>().Class("BackgroundPanel")
                .Prop(PanelContainer.StylePropertyPanel, winPanel),
            Element<PanelContainer>().Class("WindowHeadingBackground")
                .Prop(PanelContainer.StylePropertyPanel, winHeader),
            Element<Label>().Class("FancyWindowTitle")
                .Prop(Label.StylePropertyFontColor, FSUiPalette.TextPrimary),

            Element<PanelContainer>().Class(FSStyleRules.Card)
                .Prop(PanelContainer.StylePropertyPanel, cardPanel),

            Element<PanelContainer>().Class("LowDivider")
                .Prop(PanelContainer.StylePropertyPanel, divider),

            // Button label - left-aligned so ClipText clips from the right only
            Element<Label>().Class(ContainerButton.StyleClassButton)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Left),

            Element<ScrollBar>()
                .Prop(ScrollBar.StylePropertyGrabber, scrollGrab),
            Element<ScrollBar>().Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, scrollGrabHover),
            Element<ScrollBar>().Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, scrollGrabGrab),
        };

        // Tabs: active tab gets the HUD's gold underline; the page behind is the window colour, not the engine's blue-grey.
        var tabActive = Box(FSUiPalette.BgDeep, FSUiPalette.BorderPressed, 0, 12, 4);
        tabActive.BorderThickness = new Thickness(0, 0, 0, 2);
        var tabInactive = Box(FSUiPalette.BgRecess, FSUiPalette.BgRecess, 0, 12, 4);
        custom.Add(Element<TabContainer>()
            .Prop(TabContainer.StylePropertyPanelStyleBox, Box(FSUiPalette.BgDeep, null, 0, 0, 0))
            .Prop(TabContainer.StylePropertyTabStyleBox, tabActive)
            .Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabInactive)
            .Prop(TabContainer.stylePropertyTabFontColor, FSUiPalette.TextPrimary)
            .Prop(TabContainer.StylePropertyTabFontColorInactive, FSUiPalette.TextMuted)
            .Prop(Control.StylePropertyModulateSelf, Color.White));

        custom.Add(Element<LineEdit>()
            .Prop(LineEdit.StylePropertyStyleBox, Box(FSUiPalette.BgRecess, FSUiPalette.BorderNeutral, 1, 8, 4))
            .Prop(Control.StylePropertyModulateSelf, Color.White));

        custom.AddRange(FSStyleRules.Buttons(btnNormal, btnHover, btnPressed, btnDisable));
        custom.AddRange(FSStyleRules.SemanticText());

        Stylesheet = FSStyleRules.Compose(uiManager, custom);
    }
}
