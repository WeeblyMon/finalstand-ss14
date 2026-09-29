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
    public string? ItemDetail;
    public UberMeter? Uber;

    public readonly record struct UberMeter(float Charge, bool Active, float SecondsLeft, Color Color, string Key);

    private const int UberSegments = 10;
    private const float UberSegmentH = 7f;
    private static readonly Color UberGold = Color.FromHex("#FFD86B");

    public const float ScreenMargin = 24f;

    // The storage row below sizes itself from this, so it can never out-run the panel above it.
    public const float WeaponPanelWidth = 356f;

    public const int HudSlotCells = 7;
    public const float HudSlotGap = 4f;
    private const float HudSlotChrome = 8f * 2f + 2f;

    public const int HudSlotSize =
        (int) ((WeaponPanelWidth - HudSlotChrome - (HudSlotCells - 1) * HudSlotGap) / HudSlotCells);

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
        var hasDetail = !string.IsNullOrEmpty(ItemDetail);

        var name = (WeaponName ?? "no weapon").ToUpperInvariant();

        var panelW = WeaponPanelWidth * _hudScale;
        var panelPad = WeaponPanelPad * _hudScale;
        var panelMinH = WeaponPanelMinH * _hudScale;
        var hintPadX = HintPadX * _hudScale;
        var hintPadY = HintPadY * _hudScale;

        var hintH = labelH + hintPadY * 2f;
        var contentH = labelH
                       + (hasAmmo ? 4f + valueH : 0f)
                       + (hasDetail ? 4f + labelH : 0f)
                       + (WeaponTakesMagazines ? 5f + hintH : 0f)
                       + (Uber != null ? 6f + labelH + 3f + UberSegmentH * _hudScale + 5f + hintH : 0f);
        var panelH = MathF.Max(contentH + panelPad * 2f, panelMinH);

        var x = rightEdge - panelW;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var y = bottom - panelH;

        var box = new UIBox2(x, y, x + panelW, y + panelH);
        DrawPanel(screen, box, WeaponBack, WeaponEdge, FSPalette.Money);

        var textX = x + panelPad;
        var innerRight = x + panelW - panelPad;
        var ty = y + panelPad;

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

        if (hasDetail)
        {
            ty += 4f;
            screen.DrawString(_labelFont!, new Vector2(textX, ty), ItemDetail!, WeaponMuted);
            ty += labelH;
        }

        if (Uber is { } uber)
            ty = DrawUberMeter(screen, uber, textX, innerRight, ty, labelH, hintH, hintPadX, hintPadY);

        if (!WeaponTakesMagazines)
            return y;

        ty += 5f;
        const string hint = "[R] hold - ammo type";
        var hintColor = HasAmmoChoice ? HintCyan : HintDim;
        var chipW = screen.GetDimensions(_labelFont!, hint, 1f).X + hintPadX * 2f;
        var chip = new UIBox2(innerRight - chipW, ty, innerRight, ty + hintH);

        DrawRounded(screen, chip, hintColor.WithAlpha(HasAmmoChoice ? 0.10f : 0.05f), 2f);
        screen.DrawRect(chip, hintColor.WithAlpha(HasAmmoChoice ? 0.45f : 0.22f), filled: false);
        screen.DrawString(_labelFont!, new Vector2(chip.Left + hintPadX, ty + hintPadY), hint, hintColor);

        return y;
    }

    private float DrawUberMeter(DrawingHandleScreen screen, UberMeter uber, float left, float right, float ty,
        float labelH, float hintH, float hintPadX, float hintPadY)
    {
        var time = (float) _timing.RealTime.TotalSeconds;
        var ready = !uber.Active && uber.Charge >= 100f;
        var flash = 0.5f + 0.5f * MathF.Sin(time * 8f);
        var colour = ready ? Color.InterpolateBetween(UberGold, Color.White, flash * 0.6f) : uber.Color;

        ty += 6f;
        screen.DrawString(_labelFont!, new Vector2(left, ty), "ÜBERCHARGE", ready ? colour : WeaponText);

        var status = uber.Active ? $"{uber.SecondsLeft:0.0}s" : ready ? "READY" : $"{uber.Charge:0}%";
        var statusW = screen.GetDimensions(_labelFont!, status, 1f).X;
        screen.DrawString(_labelFont!, new Vector2(right - statusW, ty), status, uber.Active || ready ? colour : AmmoFull);
        ty += labelH + 3f;

        var segH = UberSegmentH * _hudScale;
        var gap = 3f * _hudScale;
        var segW = (right - left - gap * (UberSegments - 1)) / UberSegments;
        var filled = uber.Charge / 100f * UberSegments;

        for (var i = 0; i < UberSegments; i++)
        {
            var x0 = left + i * (segW + gap);
            var cell = new UIBox2(x0, ty, x0 + segW, ty + segH);
            screen.DrawRect(cell, FSPalette.BarTrack);

            var part = Math.Clamp(filled - i, 0f, 1f);
            if (part <= 0f)
                continue;

            screen.DrawRect(new UIBox2(x0, ty, x0 + segW * part, ty + segH), colour);
            screen.DrawRect(new UIBox2(x0, ty, x0 + segW * part, ty + 1f), FSPalette.BarSheen);
        }

        ty += segH + 5f;

        var hint = uber.Active ? "ÜBER ACTIVE" : $"[{uber.Key}] deploy Über";
        var hintColor = ready ? colour : HintDim;
        var chipW = screen.GetDimensions(_labelFont!, hint, 1f).X + hintPadX * 2f;
        var chip = new UIBox2(right - chipW, ty, right, ty + hintH);
        DrawRounded(screen, chip, hintColor.WithAlpha(ready ? 0.14f : 0.05f), 2f);
        screen.DrawRect(chip, hintColor.WithAlpha(ready ? 0.6f : 0.22f), filled: false);
        screen.DrawString(_labelFont!, new Vector2(chip.Left + hintPadX, ty + hintPadY), hint, hintColor);

        return ty + hintH;
    }
}
