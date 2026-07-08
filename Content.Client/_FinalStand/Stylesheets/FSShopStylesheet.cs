using System.Collections.Generic;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._FinalStand.Stylesheets;

public sealed class FSShopStylesheet
{
    public Stylesheet Stylesheet { get; }

    public FSShopStylesheet(IUserInterfaceManager uiManager)
    {
        var crust     = Color.FromHex("#0D1117");
        var mantle    = Color.FromHex("#0A0D14");
        var surface1  = Color.FromHex("#1B1F2E");
        var surface2  = Color.FromHex("#22273A");
        var overlay0  = Color.FromHex("#2A3248");
        var overlay1  = Color.FromHex("#3A4468");
        var accent    = Color.FromHex("#5E81F4");
        var accentDim = Color.FromHex("#4E71E4");
        var textMain  = Color.FromHex("#CDD6F4");

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

        var winPanel   = Box(crust,  null, 0, 0, 0);
        var winHeader  = Box(mantle, null, 0, 0, 0);
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
            // Window chrome
            Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel)
                .Prop(PanelContainer.StylePropertyPanel, winPanel),
            Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowHeader)
                .Prop(PanelContainer.StylePropertyPanel, winHeader),
            Element<Label>().Class(DefaultWindow.StyleClassWindowTitle)
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
