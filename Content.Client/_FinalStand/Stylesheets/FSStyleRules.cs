using System.Collections.Generic;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client._FinalStand.Stylesheets;

// Rule fragments shared by the FS stylesheets.
public static class FSStyleRules
{
    public const string SectionHeader = "FSSectionHeader";
    public const string Muted = "FSMuted";
    public const string Positive = "FSPositive";
    public const string Negative = "FSNegative";
    public const string Pending = "FSPending";
    public const string Currency = "FSCurrency";
    public const string Card = "FSCard";

    private static readonly Color DisabledTint = new(0.5f, 0.5f, 0.5f, 1f);

    public static IEnumerable<StyleRule> Buttons(StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox disabled)
    {
        yield return Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
            .Pseudo(ContainerButton.StylePseudoClassNormal)
            .Prop(ContainerButton.StylePropertyStyleBox, normal)
            .Prop(Control.StylePropertyModulateSelf, Color.White);

        yield return Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
            .Pseudo(ContainerButton.StylePseudoClassHover)
            .Prop(ContainerButton.StylePropertyStyleBox, hover)
            .Prop(Control.StylePropertyModulateSelf, Color.White);

        yield return Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
            .Pseudo(ContainerButton.StylePseudoClassPressed)
            .Prop(ContainerButton.StylePropertyStyleBox, pressed)
            .Prop(Control.StylePropertyModulateSelf, Color.White);

        yield return Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
            .Pseudo(ContainerButton.StylePseudoClassDisabled)
            .Prop(ContainerButton.StylePropertyStyleBox, disabled)
            .Prop(Control.StylePropertyModulateSelf, DisabledTint);
    }

    public static IEnumerable<StyleRule> SemanticText()
    {
        yield return Element<Label>().Class(SectionHeader)
            .Prop(Label.StylePropertyFontColor, FSUiPalette.AccentBrand);
        yield return Element<Label>().Class(Muted)
            .Prop(Label.StylePropertyFontColor, FSUiPalette.TextMuted);
        yield return Element<Label>().Class(Positive)
            .Prop(Label.StylePropertyFontColor, FSUiPalette.StatePositive);
        yield return Element<Label>().Class(Negative)
            .Prop(Label.StylePropertyFontColor, FSUiPalette.StateNegative);
        yield return Element<Label>().Class(Pending)
            .Prop(Label.StylePropertyFontColor, FSUiPalette.StatePending);
        yield return Element<Label>().Class(Currency)
            .Prop(Label.StylePropertyFontColor, FSUiPalette.Currency);
    }

    public static Stylesheet Compose(IUserInterfaceManager uiManager, IEnumerable<StyleRule> custom)
    {
        var baseRules = uiManager.Stylesheet?.Rules ?? Enumerable.Empty<StyleRule>();
        return new Stylesheet(baseRules.Concat(custom).ToList());
    }
}
