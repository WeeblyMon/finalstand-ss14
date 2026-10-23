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
    public string? WeaponReserveText;
    public bool HasAmmoChoice;
    public bool HasThrowChoice;
    public bool WeaponTakesMagazines;
    public string? ItemDetail;
    public UberMeter? Uber;

    public readonly record struct UberMeter(float Charge, bool Active, float SecondsLeft, Color Color, string Key,
        string Title = "ÜBERCHARGE", string Verb = "deploy Über", string ActiveHint = "ÜBER ACTIVE", string? ActiveStatus = null);

    public AbilityMeter? Ability;

    // A weapon's own resource: stored shots, a dash cooldown or rev, one segment each.
    public readonly record struct AbilityMeter(string Label, int Charges, int Max, float Refill, float SecondsLeft, Color Colour, string? Status = null);

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
                       + (Uber != null ? 6f + labelH + 3f + UberSegmentH * _hudScale + 5f + hintH : 0f)
                       + (Ability != null ? 6f + labelH + 3f + UberSegmentH * _hudScale : 0f);
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

            if (WeaponReserveText is { } reserve)
            {
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

        if (Ability is { } ability)
            ty = DrawAbilityMeter(screen, ability, textX, innerRight, ty, labelH);

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

    // A segment per charge, the next one filling as it comes back.
    private float DrawAbilityMeter(DrawingHandleScreen screen, AbilityMeter meter, float left, float right, float ty, float labelH)
    {
        ty += 6f;
        screen.DrawString(_labelFont!, new Vector2(left, ty), meter.Label, WeaponText);

        var ready = meter.Charges > 0 || meter.SecondsLeft <= 0f;
        var status = meter.Status
            ?? (!ready ? $"{MathF.Ceiling(meter.SecondsLeft * 10f) / 10f:0.0}s" : meter.Max == 1 ? Loc.GetString("fs-hud-ability-ready") : $"{meter.Charges}/{meter.Max}");
        var statusW = screen.GetDimensions(_labelFont!, status, 1f).X;
        screen.DrawString(_labelFont!, new Vector2(right - statusW, ty), status, ready ? meter.Colour : AmmoLow);
        ty += labelH + 3f;

        var segH = UberSegmentH * _hudScale;
        var gap = 3f * _hudScale;
        var count = Math.Max(1, meter.Max);
        var segW = (right - left - gap * (count - 1)) / count;
        for (var i = 0; i < count; i++)
        {
            var x0 = left + i * (segW + gap);
            screen.DrawRect(new UIBox2(x0, ty, x0 + segW, ty + segH), FSPalette.BarTrack);

            var part = i < meter.Charges ? 1f : i == meter.Charges ? meter.Refill : 0f;
            if (part <= 0f)
                continue;

            var colour = i < meter.Charges ? meter.Colour : meter.Colour.WithAlpha(0.45f);
            screen.DrawRect(new UIBox2(x0, ty, x0 + segW * part, ty + segH), colour);
            screen.DrawRect(new UIBox2(x0, ty, x0 + segW * part, ty + 1f), FSPalette.BarSheen);
        }

        return ty + segH;
    }

    private float DrawUberMeter(DrawingHandleScreen screen, UberMeter uber, float left, float right, float ty,
        float labelH, float hintH, float hintPadX, float hintPadY)
    {
        var time = (float) _timing.RealTime.TotalSeconds;
        var ready = !uber.Active && uber.Charge >= 100f;
        var flash = 0.5f + 0.5f * MathF.Sin(time * 8f);
        var colour = ready ? Color.InterpolateBetween(UberGold, Color.White, flash * 0.6f) : uber.Color;

        ty += 6f;
        screen.DrawString(_labelFont!, new Vector2(left, ty), uber.Title, ready ? colour : WeaponText);

        var status = uber.Active ? uber.ActiveStatus ?? $"{uber.SecondsLeft:0.0}s" : ready ? "READY" : $"{uber.Charge:0}%";
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

        var hint = uber.Active ? uber.ActiveHint : $"[{uber.Key}] {uber.Verb}";
        var hintColor = ready ? colour : HintDim;
        var chipW = screen.GetDimensions(_labelFont!, hint, 1f).X + hintPadX * 2f;
        var chip = new UIBox2(right - chipW, ty, right, ty + hintH);
        DrawRounded(screen, chip, hintColor.WithAlpha(ready ? 0.14f : 0.05f), 2f);
        screen.DrawRect(chip, hintColor.WithAlpha(ready ? 0.6f : 0.22f), filled: false);
        screen.DrawString(_labelFont!, new Vector2(chip.Left + hintPadX, ty + hintPadY), hint, hintColor);

        return ty + hintH;
    }
}
