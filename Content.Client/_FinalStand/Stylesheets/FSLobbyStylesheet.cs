using System.Collections.Generic;
using System.Linq;
using Content.Client.Resources;
using Content.Client.Stylesheets;
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
        var btnText  = Color.FromHex("#707070"); // nav + top-bar button labels
        var gold     = Color.FromHex("#CFA550");
        var red      = Color.FromHex("#E74C3C");
        var divider  = Color.FromHex("#2E2E2E");

        // Fonts — the lobby was rendering at the default ~12px, which read flat/small.
        var fTitle   = resCache.NotoStack("Bold", 22, display: true); // server name
        var fHeading = resCache.NotoStack("Bold", 15);                // card / section headers
        var fButton  = resCache.NotoStack("Bold", 12);                // nav tabs + top-bar buttons
        var fLabel   = resCache.NotoStack("Bold", 13);                // feature titles, tags
        var fBody    = resCache.NotoStack("Regular", 13);             // dim body text

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
        var navBarBox  = new StyleBoxFlat(Color.FromHex("#050505")); // solid black top band
        var fadeBox    = new StyleBoxFlat(new Color(0f, 0f, 0f, 0.20f)); // faint black continuation below the divider
        var scrimBox   = new StyleBoxFlat(new Color(0f, 0f, 0f, 0.35f)); // dims the splash art behind the UI
        var cardBox = NineSlice("fs_card.png", 12, 18, 16);
        var pillBox = NineSlice("fs_pill.png", 20, 18, 10);

        // Tiny-radius buttons with wide horizontal / short vertical padding
        var btnNormal   = NineSlice("fs_button_normal.png", 6, 22, 6);
        var btnHover    = NineSlice("fs_button_hover.png", 6, 22, 6);
        var btnPressed  = NineSlice("fs_button_pressed.png", 6, 22, 6);
        var btnDisabled = NineSlice("fs_button_disabled.png", 6, 22, 6);

        // Nav tabs are pure text (no box) — only the active tab's gold underline shows
        StyleBoxFlat NavBox(Color? borderBottom = null) => new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            BorderColor = borderBottom ?? Color.Transparent,
            BorderThickness = new Thickness(0, 0, 0, borderBottom == null ? 0 : 2),
            ContentMarginLeftOverride = 12,
            ContentMarginRightOverride = 12,
            ContentMarginTopOverride = 9,
            ContentMarginBottomOverride = 9,
        };
        var navNormal = NavBox();
        var navActiveBox = NavBox(gold);

        var leaveNormal  = NineSlice("fs_leave_normal.png", 6, 22, 6);
        var leaveHover   = NineSlice("fs_leave_hover.png", 6, 22, 6);
        var leavePressed = NineSlice("fs_leave_pressed.png", 6, 22, 6);

        // Observe / Join — slightly rounder than the flat top-bar buttons
        var pillbtnNormal  = NineSlice("fs_pillbtn_normal.png", 8, 16, 6);
        var pillbtnHover   = NineSlice("fs_pillbtn_hover.png", 8, 16, 6);
        var pillbtnPressed = NineSlice("fs_pillbtn_pressed.png", 8, 16, 6);

        var custom = new List<StyleRule>
        {
            // Full-bleed backdrop behind the whole lobby panel
            Element<PanelContainer>().Class("FSLobbyBackdrop")
                .Prop(PanelContainer.StylePropertyPanel, backdropBox),

            // Dark scrim over the splash art + solid black top bar
            Element<PanelContainer>().Class("FSScrim")
                .Prop(PanelContainer.StylePropertyPanel, scrimBox),
            Element<PanelContainer>().Class("FSNavBar")
                .Prop(PanelContainer.StylePropertyPanel, navBarBox),
            Element<PanelContainer>().Class("FSTopFade")
                .Prop(PanelContainer.StylePropertyPanel, fadeBox),

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

            // Nav tabs — pure text, no box, same in every state (declared after the generic rules
            // above so it wins on buttons that carry both StyleClassButton and FSNavTab)
            Element<ContainerButton>().Class("FSNavTab")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, navNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavTab")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, navNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavTab")
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, navNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),

            // Active nav tab — self-contained (LOBBY carries only this class): transparent box + gold
            // underline in every state, so the generic grey button box never shows through.
            Element<ContainerButton>().Class("FSNavActive")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, navActiveBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavActive")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, navActiveBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSNavActive")
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, navActiveBox)
                .Prop(Control.StylePropertyModulateSelf, Color.White),

            // Observe / Join pill buttons — rounder box override
            Element<ContainerButton>().Class("FSPillButton")
                .Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, pillbtnNormal)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSPillButton")
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, pillbtnHover)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class("FSPillButton")
                .Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, pillbtnPressed)
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

            // Button label — centered, small bold grey text (top-bar aesthetic)
            Element<Label>().Class(ContainerButton.StyleClassButton)
                .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center)
                .Prop(Label.StylePropertyFont, fButton)
                .Prop(Label.StylePropertyFontColor, btnText),

            // On hover, a button's label goes gold. Leave + active-nav labels set FontColorOverride in
            // code, which beats the stylesheet, so they stay put — matching "leave stays as-is on hover".
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                .Pseudo(ContainerButton.StylePseudoClassHover)
                .ParentOf(Element<Label>().Class(ContainerButton.StyleClassButton))
                .Prop(Label.StylePropertyFontColor, gold),

            // Server-info / body richtext a touch larger so it reads like the reference
            Element<RichTextLabel>().Prop(Label.StylePropertyFont, fBody),

            // Thin vertical divider (game-mode feature separators)
            Element<PanelContainer>().Class("FSVDivider")
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(divider)),

            // Text classes: font + color for card/section labels
            Element<Label>().Class("FSTitle")
                .Prop(Label.StylePropertyFont, fTitle).Prop(Label.StylePropertyFontColor, textMain),
            Element<Label>().Class("FSHeading")
                .Prop(Label.StylePropertyFont, fHeading).Prop(Label.StylePropertyFontColor, textMain),
            Element<Label>().Class("FSTextWhite")
                .Prop(Label.StylePropertyFont, fLabel).Prop(Label.StylePropertyFontColor, textMain),
            Element<Label>().Class("FSTextDim")
                .Prop(Label.StylePropertyFont, fBody).Prop(Label.StylePropertyFontColor, textDim),
            Element<Label>().Class("FSTextGold")
                .Prop(Label.StylePropertyFont, fLabel).Prop(Label.StylePropertyFontColor, gold),
            Element<Label>().Class("FSTextRed")
                .Prop(Label.StylePropertyFont, fLabel).Prop(Label.StylePropertyFontColor, red),
        };

        // Global rules first (lower indices), our overrides last (higher indices = higher priority)
        IEnumerable<StyleRule> baseRules = uiManager.Stylesheet?.Rules ?? Enumerable.Empty<StyleRule>();
        Stylesheet = new Stylesheet(baseRules.Concat(custom).ToList());
    }
}
