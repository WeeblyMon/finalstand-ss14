// Draws the active-item module: one panel for what is in the active hand, replacing vanilla's pair.
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

    // Balances the bottom band: this plus storage equals vitals plus actions, which is what puts
    // the hands on the centre line. See DefaultGameScreen's band note.
    private const float WeaponPanelW = 356f;
    private const float WeaponPanelPad = 8f;
    private const float HintPadX = 6f;
    private const float HintPadY = 3f;

    private static readonly Color WeaponBack = new(0.04f, 0.055f, 0.075f, 0.82f);
    private static readonly Color WeaponEdge = new(0.47f, 0.55f, 0.65f, 0.22f);
    private static readonly Color WeaponText = Color.FromHex("#8FA1B3");
    private static readonly Color WeaponMuted = Color.FromHex("#7c8894");
    private static readonly Color AmmoFull = Color.FromHex("#D8E0E8");
    private static readonly Color AmmoLow = Color.FromHex("#E85055");
    private static readonly Color HintCyan = Color.FromHex("#4bb8d8");
    private static readonly Color HintDim = Color.FromHex("#5c6670");

    /// <summary>Draws the module and returns its top edge, so the throwables row can sit on it.</summary>
    private float DrawWeaponModuleAndGetTop(DrawingHandleScreen screen, float margin, float bandLift)
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
                       + 5f + hintH;
        var panelH = contentH + WeaponPanelPad * 2f;

        var x = _clyde.ScreenSize.X - margin - WeaponPanelW;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var y = bottom - panelH;

        var box = new UIBox2(x, y, x + WeaponPanelW, y + panelH);
        DrawRounded(screen, box, WeaponBack);
        screen.DrawRect(box, WeaponEdge, filled: false);

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

            var reserve = WeaponReserve == 1 ? "1 mag in reserve" : $"{WeaponReserve} mags in reserve";
            var reserveW = screen.GetDimensions(_labelFont!, reserve, 1f).X;
            screen.DrawString(_labelFont!,
                new Vector2(innerRight - reserveW, ty + valueH - labelH), reserve, WeaponMuted);
            ty += valueH;
        }

        // Right-aligned chips, as in the prototype: faint cyan fill, cyan border, one per wheel.
        ty += 5f;
        var chips = new (string Text, bool Live)[]
        {
            ("[G] hold - throwable", HasThrowChoice),
            ("[R] hold - ammo type", HasAmmoChoice),
        };

        var cx = innerRight;
        foreach (var (text, live) in chips)
        {
            var w = screen.GetDimensions(_labelFont!, text, 1f).X + HintPadX * 2f;
            cx -= w;
            var chip = new UIBox2(cx, ty, cx + w, ty + hintH);
            var color = live ? HintCyan : HintDim;

            DrawRounded(screen, chip, color.WithAlpha(live ? 0.10f : 0.05f), 2f);
            screen.DrawRect(chip, color.WithAlpha(live ? 0.45f : 0.22f), filled: false);
            screen.DrawString(_labelFont!, new Vector2(cx + HintPadX, ty + HintPadY), text, color);
            cx -= 5f;
        }

        return y;
    }
}
