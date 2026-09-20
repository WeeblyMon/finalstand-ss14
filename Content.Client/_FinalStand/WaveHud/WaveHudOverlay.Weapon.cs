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

    // The inset every edge-anchored HUD module shares, so the storage row below this panel and the
    // panel itself line up on one right edge.
    public const float ScreenMargin = 24f;

    // The storage row below sizes itself from this, so it can never out-run the panel above it.
    public const float WeaponPanelWidth = 356f;

    // FINALSTAND: the one shared slot metric for every HUD grid drawn at this scale - the hotbar's
    // storage row and the action bar both read this rather than one widget reaching into another's
    // layout constants.
    public const int HudSlotCells = 7;
    public const float HudSlotGap = 4f;
    private const float HudSlotChrome = 8f * 2f + 2f;

    public const int HudSlotSize =
        (int) ((WeaponPanelWidth - HudSlotChrome - (HudSlotCells - 1) * HudSlotGap) / HudSlotCells);

    private const float WeaponPanelW = WeaponPanelWidth;
    private const float WeaponPanelPad = 8f;
    private const float WeaponPanelMinH = 46f;

    public const float RightStackMinHeight = WeaponPanelMinH + 6f + 46f;

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

    private float DrawWeaponModuleAndGetTop(DrawingHandleScreen screen, float rightEdge, float bandLift)
    {
        var labelH = _cachedLabelH;
        var valueH = _cachedValueH;
        var hasAmmo = WeaponLoaded is not null && WeaponCapacity is > 0;

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

            if (WeaponTakesMagazines)
            {
                var reserve = WeaponReserve == 1 ? "1 mag in reserve" : $"{WeaponReserve} mags in reserve";
                var reserveW = screen.GetDimensions(_labelFont!, reserve, 1f).X;
                screen.DrawString(_labelFont!,
                    new Vector2(innerRight - reserveW, ty + valueH - labelH), reserve, WeaponMuted);
            }

            ty += valueH;
        }

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
