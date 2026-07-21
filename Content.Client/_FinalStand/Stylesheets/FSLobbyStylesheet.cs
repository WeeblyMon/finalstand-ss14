using System.Collections.Generic;
using System.Linq;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._FinalStand.Stylesheets;

public sealed class FSLobbyStylesheet
{
    public Stylesheet Stylesheet { get; }

    public FSLobbyStylesheet(IUserInterfaceManager uiManager, IResourceCache resCache)
    {
        var textMain = Color.FromHex("#EDEDED");
        var textDim  = Color.FromHex("#9A9A9A");
        var gold     = Color.FromHex("#FFD700");
        var red      = Color.FromHex("#E74C3C");

        const string dir = "/Textures/_FinalStand/Interface/UI/";

        StyleBoxTexture NineSlice(string file, int margin, int hPad = 14, int vPad = 4)
        {
            var box = new StyleBoxTexture { Texture = resCache.GetTexture(dir + file) };
            box.SetPatchMargin(StyleBox.Margin.All, margin);
            box.SetContentMarginOverride(StyleBox.Margin.Horizontal, hPad);
            box.SetContentMarginOverride(StyleBox.Margin.Vertical, vPad);
            return box;
        }

        var backdropBox = new StyleBoxFlat(Color.FromHex("#0A0A0A"));
        var cardBox = NineSlice("fs_card.png", 12, 18, 16);
        var pillBox = NineSlice("fs_pill.png", 20, 18, 10);

        var btnNormal   = NineSlice("fs_button_normal.png", 10);
        var btnHover    = NineSlice("fs_button_hover.png", 10);
        var btnPressed  = NineSlice("fs_button_pressed.png", 10);
        var btnDisabled = NineSlice("fs_button_disabled.png", 10);

        // Nav tabs are flat/borderless — only a hover tint and the active tab's underline give them away
        StyleBoxFlat NavBox(Color bg, Color? borderBottom = null) => new StyleBoxFlat
        {
            BackgroundColor = bg,
            BorderColor = borderBottom ?? bg,
            BorderThickness = new Thickness(0, 0, 0, borderBottom == null ? 0 : 2),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };
        var navNormal = NavBox(Color.Transparent);
        var navHover = NavBox(Color.FromHex("#1A1A1A"));
        var navPressed = NavBox(Color.FromHex("#222222"));
        var navActiveBox = NavBox(Color.Transparent, gold);

        var leaveNormal  = NineSlice("fs_leave_normal.png", 10);
        var leaveHover   = NineSlice("fs_leave_hover.png", 10);
        var leavePressed = NineSlice("fs_leave_pressed.png", 10);

        var custom = new List<StyleRule>
        {
            // Full-bleed backdrop behind the whole lobby panel
            Element<PanelContainer>().Class("FSLobbyBackdrop")
                .Prop(PanelContainer.StylePropertyPanel, backdropBox),

            // Cards / pill bars
            Element<PanelContainer>().Class("FSLobbyCard")
                .Prop(PanelContainer.StylePropertyPanel, cardBox),
            Element<PanelContainer>().Class("FSStatusPill")
                .Prop(PanelContainer.StylePropertyPanel, pillBox),

            // Generic buttons — flat dark rounded style for all four states
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
                .Prop(ContainerButton.StylePropertyStyleBox, btnDisabled)
                .Prop(Control.StylePropertyModulateSelf, new Color(0.5f, 0.5f, 0.5f, 1f)),

            // Nav tabs — flat/borderless override (declared after the generic rules above so it wins
            // on buttons that carry both StyleClassButton and FSNavTab)
            Element<ContainerButton>().Class("FSNavTab")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, navNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavTab")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, navHover)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavTab")
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, navPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.White),

            // Active nav tab — gold underline, no fill (wins over FSNavTab since declared after it)
            Element<ContainerButton>().Class("FSNavActive")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, navActiveBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavActive")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, navActiveBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),

            // Leave button — red override
            Element<ContainerButton>().Class("FSLeaveButton")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, leaveNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSLeaveButton")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, leaveHover)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSLeaveButton")
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, leavePressed)
                .Prop(Control.StylePropertyModulateSelf, Color.White),

            // Button label — centered, standard button text color
            Element<Label>().Class(ContainerButton.StyleClassButton)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center)
                .Prop(Label.StylePropertyFontColor, textMain),

            // Text color helper classes for card/section labels
            Element<Label>().Class("FSHeading").Prop(Label.StylePropertyFontColor, textMain),
            Element<Label>().Class("FSTextDim").Prop(Label.StylePropertyFontColor, textDim),
            Element<Label>().Class("FSTextGold").Prop(Label.StylePropertyFontColor, gold),
            Element<Label>().Class("FSTextRed").Prop(Label.StylePropertyFontColor, red),
        };

        // Global rules first (lower indices), our overrides last (higher indices = higher priority)
        IEnumerable<StyleRule> baseRules = uiManager.Stylesheet?.Rules ?? Enumerable.Empty<StyleRule>();
        Stylesheet = new Stylesheet(baseRules.Concat(custom).ToList());
    }
}
