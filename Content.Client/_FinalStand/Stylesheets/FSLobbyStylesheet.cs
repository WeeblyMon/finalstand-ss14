using System.Collections.Generic;
using System.Linq;
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

        var cardBox = NineSlice("fs_card.png", 12, 12, 8);
        var pillBox = NineSlice("fs_pill.png", 20, 16, 6);

        var btnNormal   = NineSlice("fs_button_normal.png", 10);
        var btnHover    = NineSlice("fs_button_hover.png", 10);
        var btnPressed  = NineSlice("fs_button_pressed.png", 10);
        var btnDisabled = NineSlice("fs_button_disabled.png", 10);

        var navActiveBox = NineSlice("fs_nav_active.png", 10);

        var leaveNormal  = NineSlice("fs_leave_normal.png", 10);
        var leaveHover   = NineSlice("fs_leave_hover.png", 10);
        var leavePressed = NineSlice("fs_leave_pressed.png", 10);

        var custom = new List<StyleRule>
        {
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

            // Active nav tab — gold fill override (declared after the generic rules above so it wins
            // on buttons that carry both StyleClassButton and FSNavActive)
            Element<ContainerButton>().Class("FSNavActive")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
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
