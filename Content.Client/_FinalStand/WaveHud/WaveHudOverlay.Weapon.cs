// Draws the active-item module: one panel for what is in the active hand, replacing vanilla's pair.
using Content.Client._FinalStand.Interface;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public string? WeaponName;
    public int? WeaponLoaded;
    public int? WeaponCapacity;
    public int WeaponReserve;
    public bool HasAmmoChoice;
    public bool HasThrowChoice;
    public bool WeaponTakesMagazines;

    // Balances the bottom band: this plus storage equals vitals plus actions, which is what puts
    // the hands on the centre line. See DefaultGameScreen's band note.
    private const float WeaponPanelW = 356f;
    private const float WeaponPanelPad = 8f;
    private const float WeaponPanelMinH = 46f;

    /// <summary>Weapon module plus the throwables row above it, at their smallest. Only a starting
    /// value - the module grows with the equipped weapon, so read <see cref="RightStackHeight"/>.</summary>
    public const float RightStackMinHeight = WeaponPanelMinH + 6f + 46f;

    /// <summary>Measured height of that stack above the band, as last drawn.</summary>
    public float RightStackHeight = RightStackMinHeight;
    private const float HintPadX = 6f;
    private const float HintPadY = 3f;

    private static readonly Color WeaponBack = FSPalette.PanelBack;
    private static readonly Color WeaponEdge = FSPalette.PanelEdge;
    private static readonly Color WeaponText = FSPalette.TextMuted;
    private static readonly Color WeaponMuted = FSPalette.TextDim;
    private static readonly Color AmmoFull = FSPalette.TextBright;
    private static readonly Color AmmoLow = FSPalette.Danger;
    private static readonly Color HintCyan = FSPalette.Money;
    private static readonly Color HintDim = FSPalette.TextDim;

    /// <summary>Draws the module and returns its top edge, so the throwables row can sit on it.</summary>
    private float DrawWeaponModuleAndGetTop(DrawingHandleScreen screen, float rightEdge, float bandLift)
    {
        var labelH = _cachedLabelH;
        var valueH = _cachedValueH;
        var hasAmmo = WeaponLoaded is not null && WeaponCapacity is > 0;

        // An empty hand still gets the panel. A module that vanishes takes the band's balance with
        // it, and leaves the player wondering whether the HUD broke.
        var name = (WeaponName ?? "no weapon").ToUpperInvariant();

        var hintH = labelH + HintPadY * 2f;
        var contentH = labelH
                       + (hasAmmo ? 4f + valueH : 0f)
                       + (WeaponTakesMagazines ? 5f + hintH : 0f);
        var panelH = MathF.Max(contentH + WeaponPanelPad * 2f, WeaponPanelMinH);

        var x = rightEdge - WeaponPanelW;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var y = bottom - panelH;

        var box = new UIBox2(x, y, x + WeaponPanelW, y + panelH);
        DrawPanel(screen, box, WeaponBack, WeaponEdge, FSPalette.Money);

        var textX = x + WeaponPanelPad;
        var innerRight = x + WeaponPanelW - WeaponPanelPad;
        var ty = y + WeaponPanelPad;

        screen.DrawString(_labelFont!, new Vector2(textX, ty), name,
            WeaponName is null ? WeaponMuted : WeaponText);
        ty += labelH;

        if (hasAmmo)
        {
            ty += 4f;
            var loaded = WeaponLoaded!.Value;
            var capacity = WeaponCapacity!.Value;
            var ammoColor = loaded == 0 || loaded <= capacity * 0.25f ? AmmoLow : AmmoFull;

            screen.DrawString(_valueFont!, new Vector2(textX, ty), $"{loaded}/{capacity}", ammoColor);

            // Only guns that take magazines have a reserve. A revolver or a laser cell saying
            // "0 mags in reserve" reads as a fault rather than a fact.
            if (WeaponTakesMagazines)
            {
                var reserve = WeaponReserve == 1 ? "1 mag in reserve" : $"{WeaponReserve} mags in reserve";
                var reserveW = screen.GetDimensions(_labelFont!, reserve, 1f).X;
                screen.DrawString(_labelFont!,
                    new Vector2(innerRight - reserveW, ty + valueH - labelH), reserve, WeaponMuted);
            }

            ty += valueH;
        }

        // One chip, right-aligned, as in the prototype. The throwable hint belongs to the throwables
        // row above rather than here - two hold-hints in one panel read as one control with two keys.
        if (!WeaponTakesMagazines)
            return y;

        ty += 5f;
        const string hint = "[R] hold - ammo type";
        var hintColor = HasAmmoChoice ? HintCyan : HintDim;
        var chipW = screen.GetDimensions(_labelFont!, hint, 1f).X + HintPadX * 2f;
        var chip = new UIBox2(innerRight - chipW, ty, innerRight, ty + hintH);

        DrawRounded(screen, chip, hintColor.WithAlpha(HasAmmoChoice ? 0.10f : 0.05f), 2f);
        screen.DrawRect(chip, hintColor.WithAlpha(HasAmmoChoice ? 0.45f : 0.22f), filled: false);
        screen.DrawString(_labelFont!, new Vector2(chip.Left + HintPadX, ty + HintPadY), hint, hintColor);

        return y;
    }
}
