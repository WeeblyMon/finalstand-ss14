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

// Shared stylesheet for every FS menu window (Weapon Shop, Research Computer, ...) - buttons,
// scrollbars and window chrome are keyed off engine-generic style classes so one Stylesheet
// object restyles all of them consistently, sourced from FSUiPalette's tokens.
public sealed class FSMenuStylesheet
{
    public Stylesheet Stylesheet { get; }

    public FSMenuStylesheet(IUserInterfaceManager uiManager, IResourceCache resCache)
    {
        var crust     = FSUiPalette.BgDeep;
        var mantle    = FSUiPalette.BgRecess;
        var surface1  = FSUiPalette.BgSurface;
        var surface2  = Color.FromHex("#1E2536");
        var overlay0  = FSUiPalette.BorderNeutral;
        var overlay1  = Color.FromHex("#647089");
        var accent    = FSUiPalette.AccentBrand;
        var accentDim = Color.FromHex("#6B74D6");
        var textMain  = FSUiPalette.TextPrimary;

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

        StyleBoxTexture RoundedPanel(Color tint) => new()
        {
            Texture = resCache.GetResource<TextureResource>("/Textures/_FinalStand/Interface/Research/panel_bg.png").Texture,
            Modulate = tint,
            PatchMarginLeft = FSUiPalette.PanelCornerRadius,
            PatchMarginRight = FSUiPalette.PanelCornerRadius,
            PatchMarginTop = FSUiPalette.PanelCornerRadius,
            PatchMarginBottom = FSUiPalette.PanelCornerRadius,
        };

        var winPanel   = RoundedPanel(crust);
        // header stays a flat rectangle, not rounded - a rounded title bar at the very top edge
        // of a window reads as a floating pill rather than window chrome
        var winHeader  = Box(mantle);
        var btnNormal  = Box(surface1, overlay0, 1);
        var btnHover   = Box(surface2, accent,   1);
        var btnPressed = Box(Color.FromHex("#181B2B"), accentDim, 1);
        var btnDisable = Box(Color.FromHex("#0F1018"), Color.FromHex("#1A1D28"), 1);
        var divider    = new StyleBoxFlat
        {
            BackgroundColor             = overlay0,
            ContentMarginTopOverride    = 1,
            ContentMarginBottomOverride = 1,
        };
        var scrollGrab      = Box(overlay0, null, 0, 3, 3);
        var scrollGrabHover = Box(overlay1, null, 0, 3, 3);
        var scrollGrabGrab  = Box(accent,   null, 0, 3, 3);

        var custom = new List<StyleRule>
        {
            // Window chrome - DefaultWindow (Weapon Shop) uses one set of style classes,
            // FancyWindow (Research Computer) uses a completely different set for the same
            // roles, so both need their own rule pointing at the same tokens/panels.
            Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, winPanel),
            Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowHeader)
                .Prop(PanelContainer.StylePropertyPanel, winHeader),
            Element<Label>().Class(DefaultWindow.StyleClassWindowTitle)
                .Prop(Label.StylePropertyFontColor, textMain),

            Element<PanelContainer>().Class("BackgroundPanel")
                .Prop(PanelContainer.StylePropertyPanel, winPanel),
            Element<PanelContainer>().Class("WindowHeadingBackground")
                .Prop(PanelContainer.StylePropertyPanel, winHeader),
            Element<Label>().Class("FancyWindowTitle")
                .Prop(Label.StylePropertyFontColor, textMain),

            // Thin horizontal rule
            Element<PanelContainer>().Class("LowDivider")
                .Prop(PanelContainer.StylePropertyPanel, divider),

            // Buttons — flat dark style for all four states
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, btnNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, btnHover)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, btnPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(ContainerButton.StylePropertyStyleBox, btnDisable)
                .Prop(Control.StylePropertyModulateSelf, new Color(0.5f, 0.5f, 0.5f, 1f)),

            // Button label — left-aligned so ClipText clips from the right only
            Element<Label>().Class(ContainerButton.StyleClassButton)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Left),

            // Scrollbar grabber
            Element<ScrollBar>()
                .Prop(ScrollBar.StylePropertyGrabber, scrollGrab),
            Element<ScrollBar>().Pseudo(ScrollBar.StylePseudoClassHover)
                .Prop(ScrollBar.StylePropertyGrabber, scrollGrabHover),
            Element<ScrollBar>().Pseudo(ScrollBar.StylePseudoClassGrabbed)
                .Prop(ScrollBar.StylePropertyGrabber, scrollGrabGrab),
        };

        // Global rules first (lower indices), our overrides last (higher indices = higher priority)
        IEnumerable<StyleRule> baseRules = uiManager.Stylesheet?.Rules ?? Enumerable.Empty<StyleRule>();
        Stylesheet = new Stylesheet(baseRules.Concat(custom).ToList());
    }
}
