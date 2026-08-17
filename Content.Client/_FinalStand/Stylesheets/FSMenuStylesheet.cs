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
    private const string PanelTexture = "/Textures/_FinalStand/Interface/Research/panel_bg.png";

    private static Stylesheet? _cached;

    public Stylesheet Stylesheet { get; }

    // Built once. The rules hold colours and textures only, so every FS window can share one instance.
    public static Stylesheet Get(IUserInterfaceManager uiManager, IResourceCache resCache)
        => _cached ??= new FSMenuStylesheet(uiManager, resCache).Stylesheet;

    public static StyleBoxTexture CardPanel(IResourceCache resCache, Color tint)
    {
        var box = new StyleBoxTexture
        {
            Texture = resCache.GetResource<TextureResource>(PanelTexture).Texture,
            Modulate = tint,
        };
        box.SetPatchMargin(StyleBox.Margin.All, FSUiPalette.PanelCornerRadius);
        return box;
    }

    public FSMenuStylesheet(IUserInterfaceManager uiManager, IResourceCache resCache)
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

        var winPanel   = CardPanel(resCache, FSUiPalette.BgDeep);
        var cardPanel  = CardPanel(resCache, FSUiPalette.BgSurface);
        var winHeader  = Box(FSUiPalette.BgRecess);
        var btnNormal  = Box(FSUiPalette.BgSurface, FSUiPalette.BorderNeutral, 1);
        var btnHover   = Box(FSUiPalette.BgElevated, FSUiPalette.AccentBrand, 1);
        var btnPressed = Box(FSUiPalette.BgPressed, FSUiPalette.AccentBrandDim, 1);
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

        custom.AddRange(FSStyleRules.Buttons(btnNormal, btnHover, btnPressed, btnDisable));
        custom.AddRange(FSStyleRules.SemanticText());

        Stylesheet = FSStyleRules.Compose(uiManager, custom);
    }
}
