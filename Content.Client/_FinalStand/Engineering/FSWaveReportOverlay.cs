// When a wave ends, a card slides in near the top of the screen with what your line did: damage soaked, HP repaired,
// equipment kills and credits earned, counting up, then slides away.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Engineering;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Engineering;

public sealed partial class FSWaveReportOverlay : Overlay
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly ResPath LabelPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly ResPath NumberPath = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf");
    private static readonly Color Back = FSPalette.PanelDeep.WithAlpha(0.92f);
    private static readonly Color Shadow = Color.Black.WithAlpha(0.35f);

    private const float Width = 380f;
    private const float Height = 74f;
    private const float Slant = 14f;
    private const double SlideTime = 0.3;
    private const double HoldTime = 6.5;
    private const double CountTime = 0.9;

    private FSEngineerWaveReportEvent? _report;
    private TimeSpan _shownAt;
    private Font? _label;
    private Font? _number;
    private float _fontScale;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSWaveReportOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Show(FSEngineerWaveReportEvent report)
    {
        _report = report;
        _shownAt = _timing.RealTime;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_report is not { } report)
            return;

        var age = (_timing.RealTime - _shownAt).TotalSeconds;
        if (age > HoldTime + SlideTime)
        {
            _report = null;
            return;
        }

        var s = _ui.DefaultUIScale;
        LoadFonts(s);
        var slideIn = Ease((float) Math.Clamp(age / SlideTime, 0d, 1d));
        var slideOut = Ease((float) Math.Clamp((age - HoldTime) / SlideTime, 0d, 1d));
        var alpha = slideIn * (1f - slideOut);
        var count = Ease((float) Math.Clamp((age - SlideTime) / CountTime, 0d, 1d));

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var width = MathF.Round(Width * s);
        var height = MathF.Round(Height * s);
        var slant = MathF.Round(Slant * s);
        var left = MathF.Round(bounds.Center.X - width / 2f + (1f - slideIn) * 60f * s - slideOut * 60f * s);
        var top = MathF.Round(bounds.Top + bounds.Height * 0.16f);
        var topLeft = new Vector2(left, top);

        FSShapes.Slanted(handle, topLeft + new Vector2(3f, 4f) * s, width, height, slant, Fade(Shadow, alpha));
        FSShapes.Slanted(handle, topLeft, width, height, slant, Fade(Back, alpha));
        FSShapes.Slanted(handle, topLeft, width, height, slant, Fade(FSPalette.ChipEdge, alpha), border: MathF.Max(1f, s));
        FSShapes.Slanted(handle, topLeft + new Vector2(2f * s, 0f), slant + 4f * s, height, slant, Fade(FSPalette.Money, alpha));

        var x = left + slant + 14f * s;
        var title = Loc.GetString("fs-wave-report-title", ("wave", report.Wave));
        handle.DrawString(_label!, new Vector2(x, top + 8f * s), title, Fade(FSPalette.Money, alpha));

        var columns = new (string Caption, int Value, string Prefix, Color Color)[]
        {
            (Loc.GetString("fs-wave-report-soaked"), report.Absorbed, "", FSPalette.TextBright),
            (Loc.GetString("fs-wave-report-repaired"), report.Repaired, "+", FSPalette.Ok),
            (Loc.GetString("fs-wave-report-kills"), report.Kills, "", FSPalette.TextBright),
            (Loc.GetString("fs-wave-report-earned"), report.Credits, "$", FSPalette.Money),
        };

        var columnWidth = (width - slant * 2f - 28f * s) / columns.Length;
        var rowTop = top + 24f * s;
        for (var i = 0; i < columns.Length; i++)
        {
            var (caption, value, prefix, color) = columns[i];
            var cx = x + i * columnWidth;
            var shown = (int) MathF.Round(value * count);
            handle.DrawString(_number!, new Vector2(cx, rowTop), prefix + shown.ToString("N0"), Fade(color, alpha));
            handle.DrawString(_label!, new Vector2(cx, rowTop + _number!.GetLineHeight(1f) - 2f * s), caption,
                Fade(FSPalette.TextMuted, alpha));
        }
    }

    private void LoadFonts(float s)
    {
        if (_label != null && Math.Abs(_fontScale - s) < 0.01f)
            return;

        _fontScale = s;
        _label = new VectorFont(_cache.GetResource<FontResource>(LabelPath), (int) MathF.Round(7 * s));
        _number = new VectorFont(_cache.GetResource<FontResource>(NumberPath), (int) MathF.Round(15 * s));
    }

    private static Color Fade(Color color, float alpha) => color.WithAlpha(color.A * alpha);

    private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
}
