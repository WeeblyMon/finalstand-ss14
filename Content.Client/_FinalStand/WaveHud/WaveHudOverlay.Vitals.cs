// Draws the bottom-left vitals panel: health figure and bar, stamina bar, then status pills.
using Content.Client._FinalStand.Interface;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public float? HealthRatio;
    public bool HealthInCrit;
    public float? StaminaRatio;
    public int HealthCurrent;
    public int HealthMax;
    public string? DownText;
    public bool DownHeld;
    public readonly List<float> HealthTicks = new();
    public readonly List<VitalPill> StatusPills = new();

    public enum VitalPillStyle : byte
    {
        Active,
        Ward,
        Empty,
        Status,
    }

    public readonly record struct VitalPill(string Text, Color Color, Texture? Icon, float Bleed, float Fill,
        byte Warning, VitalPillStyle Style);

    public const float BottomBandLift = 23f;

    private const float TopLeftX = 10f;
    private const float TopLeftY = 34f;
    private const float TopLeftPad = 8f;

    private const float TopLeftYSeparated = 150f;

    // Raw-pixel drawing does not inherit engine UIScale, so these read _hudScale directly.
    public const float VitalsWidthBase = 240f;
    private const float VitalsPadXBase = 8f;
    private const float VitalsPadYBase = 7f;
    private const float HpBarHBase = 12f;
    private const float StaminaBarHBase = 8f;
    private const float VitalsRowGapBase = 6f;
    private const float PillPadXBase = 5f;
    private const float PillPadYBase = 2f;
    private const float PillGapBase = 3f;
    private const float BleedPipWBase = 4f;
    private const int BleedPips = 3;

    private float VitalsWidth => VitalsWidthBase * _hudScale;
    private float VitalsPadX => VitalsPadXBase * _hudScale;
    private float VitalsPadY => VitalsPadYBase * _hudScale;
    private float HpBarH => HpBarHBase * _hudScale;
    private float StaminaBarH => StaminaBarHBase * _hudScale;
    private float VitalsRowGap => VitalsRowGapBase * _hudScale;
    private float PillPadX => PillPadXBase * _hudScale;
    private float PillPadY => PillPadYBase * _hudScale;
    private float PillGap => PillGapBase * _hudScale;
    private float BleedPipW => BleedPipWBase * _hudScale;

    private static readonly Color VitalsBack = FSPalette.PanelBack;
    private static readonly Color VitalsEdge = FSPalette.PanelEdge;
    private static readonly Color VitalsEdgeHot = FSPalette.PanelEdgeHot;
    private static readonly Color BarTrack = FSPalette.BarTrack;
    private static readonly Color HpFill = FSPalette.HealthFill;
    private static readonly Color HpFillLow = FSPalette.HealthLow;
    private static readonly Color StaminaFill = FSPalette.StaminaFill;
    private static readonly Color StaminaFillLow = FSPalette.StaminaLow;
    private static readonly Color PillBack = FSPalette.CellBack;
    private static readonly Color VitalsMuted = FSPalette.TextMuted;

    public static void DrawRounded(DrawingHandleScreen screen, UIBox2 box, Color color, float radius = 3f)
    {
        var r = MathF.Min(radius, MathF.Min(box.Width, box.Height) * 0.5f);
        screen.DrawRect(new UIBox2(box.Left + r, box.Top, box.Right - r, box.Bottom), color);
        screen.DrawRect(new UIBox2(box.Left, box.Top + r, box.Left + r, box.Bottom - r), color);
        screen.DrawRect(new UIBox2(box.Right - r, box.Top + r, box.Right, box.Bottom - r), color);
    }

    private static readonly Color PanelShadow = FSPalette.PanelShadow;
    private static readonly Color PanelSheen = FSPalette.PanelSheen;
    private static readonly Color PanelBevel = Color.Black.WithAlpha(0.30f);

    private const float Chamfer = 3f;

    public static void DrawPanel(DrawingHandleScreen screen, UIBox2 box, Color fill, Color edge,
        Color? accent = null)
    {
        DrawChamfered(screen, new UIBox2(box.Left + 2f, box.Top + 2f, box.Right + 2f, box.Bottom + 2f),
            PanelShadow);
        DrawChamfered(screen, box, fill);

        var lip = new UIBox2(box.Left + Chamfer, box.Top, box.Right - Chamfer, box.Top + 1f);
        if (accent is { } a)
        {
            screen.DrawRect(new UIBox2(lip.Left, lip.Top, lip.Right, lip.Top + 2f), a);
        }
        else
        {
            screen.DrawRect(lip, PanelSheen);
        }
        screen.DrawRect(new UIBox2(box.Left + Chamfer, box.Bottom - 1f, box.Right - Chamfer, box.Bottom), PanelBevel);

        DrawChamferedEdge(screen, box, edge);
    }

    public static void DrawChamfered(DrawingHandleScreen screen, UIBox2 box, Color color)
    {
        var c = MathF.Min(Chamfer, MathF.Min(box.Width, box.Height) * 0.5f);

        screen.DrawRect(new UIBox2(box.Left + c, box.Top, box.Right - c, box.Bottom), color);
        screen.DrawRect(new UIBox2(box.Left, box.Top + c, box.Left + c, box.Bottom - c), color);
        screen.DrawRect(new UIBox2(box.Right - c, box.Top + c, box.Right, box.Bottom - c), color);

        for (var i = 0; i < c; i++)
        {
            var inset = c - i;
            screen.DrawRect(new UIBox2(box.Left + inset, box.Top + i, box.Left + c, box.Top + i + 1f), color);
            screen.DrawRect(new UIBox2(box.Right - c, box.Top + i, box.Right - inset, box.Top + i + 1f), color);
            screen.DrawRect(new UIBox2(box.Left + inset, box.Bottom - i - 1f, box.Left + c, box.Bottom - i), color);
            screen.DrawRect(new UIBox2(box.Right - c, box.Bottom - i - 1f, box.Right - inset, box.Bottom - i), color);
        }
    }

    private static void DrawChamferedEdge(DrawingHandleScreen screen, UIBox2 box, Color edge)
    {
        var c = MathF.Min(Chamfer, MathF.Min(box.Width, box.Height) * 0.5f);

        screen.DrawLine(new Vector2(box.Left + c, box.Top), new Vector2(box.Right - c, box.Top), edge);
        screen.DrawLine(new Vector2(box.Left + c, box.Bottom), new Vector2(box.Right - c, box.Bottom), edge);
        screen.DrawLine(new Vector2(box.Left, box.Top + c), new Vector2(box.Left, box.Bottom - c), edge);
        screen.DrawLine(new Vector2(box.Right, box.Top + c), new Vector2(box.Right, box.Bottom - c), edge);

        screen.DrawLine(new Vector2(box.Left, box.Top + c), new Vector2(box.Left + c, box.Top), edge);
        screen.DrawLine(new Vector2(box.Right - c, box.Top), new Vector2(box.Right, box.Top + c), edge);
        screen.DrawLine(new Vector2(box.Left, box.Bottom - c), new Vector2(box.Left + c, box.Bottom), edge);
        screen.DrawLine(new Vector2(box.Right - c, box.Bottom), new Vector2(box.Right, box.Bottom - c), edge);
    }

    public static void DrawShadowed(DrawingHandleScreen screen, Font font, Vector2 pos, string text, Color color)
    {
        screen.DrawString(font, pos + new Vector2(1f, 1f), text, new Color(0f, 0f, 0f, 0.85f));
        screen.DrawString(font, pos, text, color);
    }

    private float _pillRowH = 17f;
    private int _pillRows;

    public float VitalsBlockHeight()
    {
        if (HealthRatio is null)
            return 0f;

        var h = VitalsPadY * 2f
                + _cachedValueH + 4f + HpBarH
                + VitalsRowGap + StaminaBarH;

        if (_pillRows > 0)
            h += VitalsRowGap + _pillRows * _pillRowH + (_pillRows - 1) * PillGap;

        return h;
    }

    private int MeasurePillRows(DrawingHandleScreen screen, float innerW)
    {
        if (StatusPills.Count == 0)
            return 0;

        var rows = 1;
        var used = 0f;

        foreach (var pill in StatusPills)
        {
            var w = PillWidth(screen, pill);

            if (used > 0f && used + PillGap + w > innerW)
            {
                rows++;
                used = w;
                continue;
            }

            used += (used > 0f ? PillGap : 0f) + w;
        }

        return rows;
    }

    private void DrawVitals(DrawingHandleScreen screen, float margin, float bandLift)
    {
        if (HealthRatio is not { } health)
            return;

        _pillRowH = _cachedLabelH + PillPadY * 2f;
        _pillRows = MeasurePillRows(screen, VitalsWidth - VitalsPadX * 2f);

        var panelH = VitalsBlockHeight();
        var x = margin;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var top = bottom - panelH;

        var lowHp = health <= 0.3f || HealthInCrit;

        var panelBox = new UIBox2(x, top, x + VitalsWidth, bottom);
        DrawPanel(screen, panelBox, VitalsBack, lowHp ? VitalsEdgeHot : VitalsEdge,
            lowHp ? FSPalette.HealthLow : FSPalette.HealthFill);

        var innerX = x + VitalsPadX;
        var innerW = VitalsWidth - VitalsPadX * 2f;
        var y = top + VitalsPadY;

        var hpText = DownText ?? HealthCurrent.ToString();
        var hpColor = DownText != null ? (DownHeld ? FSPalette.Ok : FSPalette.Danger) : lowHp ? HpFillLow : FSPalette.TextBright;
        var hpDims = screen.GetDimensions(_valueFont!, hpText, 1f);
        var rowH = MathF.Max(hpDims.Y, HpBarH);
        screen.DrawString(_valueFont!, new Vector2(innerX, y + (rowH - hpDims.Y) * 0.5f), hpText, hpColor);

        if (DownText == null)
        {
            const string healthWord = "HEALTH";
            var wordX = innerX + hpDims.X + 7f;
            screen.DrawString(_labelFont!, new Vector2(wordX, y + (rowH - _cachedLabelH) * 0.5f),
                healthWord, VitalsMuted);
        }

        y += rowH + 4f;

        DrawBar(screen, innerX, y, innerW, HpBarH, health, lowHp ? HpFillLow : HpFill);
        foreach (var tick in HealthTicks)
        {
            var tickX = innerX + MathF.Round(innerW * tick);
            screen.DrawRect(new UIBox2(tickX, y, tickX + 1f, y + HpBarH), FSPalette.TextBright.WithAlpha(0.55f));
        }

        y += HpBarH;

        if (StaminaRatio is { } stamina)
        {
            y += VitalsRowGap;
            DrawBar(screen, innerX, y, innerW, StaminaBarH, stamina,
                stamina <= 0.35f ? StaminaFillLow : StaminaFill);
            y += StaminaBarH;
        }

        if (_pillRows > 0)
            DrawPills(screen, innerX, y + VitalsRowGap, innerW);
    }

    private float PillWidth(DrawingHandleScreen screen, VitalPill pill)
    {
        var w = screen.GetDimensions(_labelFont!, pill.Text, 1f).X + PillPadX * 2f;
        if (pill.Icon != null)
            w += _pillRowH - PillPadY * 2f + PillGap;

        if (pill.Bleed > 0f)
            w += PillGap + BleedPips * (BleedPipW + 1f);

        return w;
    }

    private void DrawPills(DrawingHandleScreen screen, float left, float top, float innerW)
    {
        var px = left;
        var py = top;

        foreach (var pill in StatusPills)
        {
            var w = PillWidth(screen, pill);

            if (px > left && px + w > left + innerW)
            {
                px = left;
                py += _pillRowH + PillGap;
            }

            var box = new UIBox2(px, py, px + w, py + _pillRowH);
            DrawPill(screen, box, pill);

            px += w + PillGap;
        }
    }

    private void DrawPill(DrawingHandleScreen screen, UIBox2 box, VitalPill pill)
    {
        DrawRounded(screen, box, PillBack, 2f);

        if (pill.Fill > 0f)
            screen.DrawRect(new UIBox2(box.Left, box.Top, box.Left + box.Width * pill.Fill, box.Bottom), pill.Color.WithAlpha(0.22f));

        var textColor = pill.Style switch
        {
            VitalPillStyle.Empty => FSPalette.TextDim,
            _ => pill.Color,
        };

        var edge = pill.Style switch
        {
            VitalPillStyle.Empty => FSPalette.CellEdge,
            VitalPillStyle.Ward or VitalPillStyle.Status => pill.Color,
            _ => pill.Color.WithAlpha(0.45f),
        };
        screen.DrawRect(box, edge, filled: false);

        if (pill.Warning > 0)
        {
            var warn = pill.Color.WithAlpha(pill.Warning > 1 ? 0.95f : 0.6f);
            screen.DrawRect(box, warn, filled: false);
            if (pill.Warning > 1)
                screen.DrawRect(new UIBox2(box.Left + 1f, box.Top + 1f, box.Right - 1f, box.Bottom - 1f), warn, filled: false);
        }

        var x = box.Left + PillPadX;
        if (pill.Icon is { } icon)
        {
            var size = box.Height - PillPadY * 2f;
            screen.DrawTextureRect(icon, new UIBox2(x, box.Top + PillPadY, x + size, box.Top + PillPadY + size));
            x += size + PillGap;
        }

        screen.DrawString(_labelFont!, new Vector2(x, box.Top + PillPadY), pill.Text, textColor);

        if (pill.Bleed <= 0f)
            return;

        var pipX = box.Right - PillPadX - BleedPips * (BleedPipW + 1f) + 1f;
        var pipTop = box.Top + PillPadY + 1f;
        var pipBottom = box.Bottom - PillPadY - 1f;
        for (var i = 0; i < BleedPips; i++)
        {
            var pipBox = new UIBox2(pipX, pipTop, pipX + BleedPipW, pipBottom);
            screen.DrawRect(pipBox, FSPalette.BarTrack);

            var fill = Math.Clamp(pill.Bleed - i, 0f, 1f);
            if (fill > 0f)
                screen.DrawRect(new UIBox2(pipBox.Left, pipBottom - (pipBottom - pipTop) * fill, pipBox.Right, pipBottom), pill.Color);

            pipX += BleedPipW + 1f;
        }
    }

    private static void DrawBar(DrawingHandleScreen screen, float x, float y, float w, float h, float ratio, Color fill)
    {
        screen.DrawRect(new UIBox2(x, y, x + w, y + h), BarTrack);

        var filled = MathF.Round(w * Math.Clamp(ratio, 0f, 1f));
        if (filled > 0f)
        {
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h), fill);
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h * 0.35f), FSPalette.BarSheen);
        }

        screen.DrawRect(new UIBox2(x, y, x + w, y + h), VitalsEdge, filled: false);
    }
}
