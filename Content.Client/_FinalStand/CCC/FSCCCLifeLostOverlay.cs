using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.CCC;

public sealed partial class FSCCCLifeLostOverlay : Overlay
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private static readonly ResPath TitlePath = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf");
    private static readonly ResPath LabelPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly Color Alarm = Color.FromHex("#e4512f");
    private const double Duration = 4.0;

    private TimeSpan _shownAt;
    private int _left;
    private int _lives;
    private bool _active;
    private Font? _title;
    private Font? _label;
    private float _fontScale;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSCCCLifeLostOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Show(int left, int lives)
    {
        _left = left;
        _lives = lives;
        _shownAt = _timing.RealTime;
        _active = true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_active)
            return;

        var age = (_timing.RealTime - _shownAt).TotalSeconds;
        if (age > Duration)
        {
            _active = false;
            return;
        }

        var s = _ui.DefaultUIScale;
        LoadFonts(s);
        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var fadeOut = (float) Math.Clamp((Duration - age) / 0.6, 0d, 1d);

        var pulse = (float) (0.5 + 0.5 * Math.Cos(age * Math.PI * 3)) * (float) Math.Clamp(1.6 - age / 1.5, 0d, 1d);
        DrawEdges(handle, bounds, Alarm.WithAlpha(0.55f * pulse * fadeOut), 90f * s);

        var pop = (float) Math.Clamp(age / 0.25, 0d, 1d);
        var scale = 1f + (1f - pop) * 0.4f;
        var center = new Vector2(bounds.Center.X, bounds.Top + bounds.Height * 0.28f);

        var title = Loc.GetString("fs-ccc-lives-banner-title");
        var titleSize = handle.GetDimensions(_title!, title, scale);
        var width = MathF.Max(titleSize.X + 120f * s, 420f * s);
        var height = 118f * s;
        var topLeft = center - new Vector2(width / 2f, height / 2f);
        FSShapes.Slanted(handle, topLeft + new Vector2(3f, 4f) * s, width, height, 18f * s, Color.Black.WithAlpha(0.35f * fadeOut));
        FSShapes.Slanted(handle, topLeft, width, height, 18f * s, FSPalette.PanelDeep.WithAlpha(0.92f * fadeOut));
        FSShapes.Slanted(handle, topLeft, width, height, 18f * s, Alarm.WithAlpha(fadeOut), border: 2f * s);

        handle.DrawString(_title!, new Vector2(center.X - titleSize.X / 2f, topLeft.Y + 12f * s), title, scale, Alarm.WithAlpha(fadeOut));

        var sub = Loc.GetString(_left == 1 ? "fs-ccc-lives-banner-last" : "fs-ccc-lives-banner-left", ("left", _left));
        var subSize = handle.GetDimensions(_label!, sub, 1f);
        handle.DrawString(_label!, new Vector2(center.X - subSize.X / 2f, topLeft.Y + height - 50f * s), sub, FSPalette.TextBright.WithAlpha(fadeOut));

        var spacing = 26f * s;
        var start = center.X - (_lives - 1) * spacing / 2f;
        var pipY = topLeft.Y + height - 16f * s;
        for (var i = 0; i < _lives; i++)
        {
            var at = new Vector2(start + i * spacing, pipY);
            var lost = i >= _left;
            var color = lost ? FSPalette.TextMuted.WithAlpha(0.4f * fadeOut) : Alarm.WithAlpha(fadeOut);
            if (i == _left && age < 1.2)
                color = Color.White.WithAlpha((float) (1 - age / 1.2) * fadeOut);
            FSShapes.Diamond(handle, at, 16f * s, color, border: lost ? 1.5f * s : 0f);
        }
    }

    private static void DrawEdges(DrawingHandleScreen handle, UIBox2i bounds, Color color, float depth)
    {
        const int steps = 8;
        var w = depth / steps;
        for (var i = 0; i < steps; i++)
        {
            var inset = depth * i / steps;
            var shade = color.WithAlpha(color.A * (1f - (float) i / steps) * 2f / steps);
            handle.DrawRect(new UIBox2(bounds.Left + inset, bounds.Top, bounds.Left + inset + w, bounds.Bottom), shade);
            handle.DrawRect(new UIBox2(bounds.Right - inset - w, bounds.Top, bounds.Right - inset, bounds.Bottom), shade);
            handle.DrawRect(new UIBox2(bounds.Left, bounds.Top + inset, bounds.Right, bounds.Top + inset + w), shade);
            handle.DrawRect(new UIBox2(bounds.Left, bounds.Bottom - inset - w, bounds.Right, bounds.Bottom - inset), shade);
        }
    }

    private void LoadFonts(float s)
    {
        if (_title != null && Math.Abs(_fontScale - s) < 0.01f)
            return;

        _fontScale = s;
        _title = new VectorFont(_cache.GetResource<FontResource>(TitlePath), (int) MathF.Round(30 * s));
        _label = new VectorFont(_cache.GetResource<FontResource>(LabelPath), (int) MathF.Round(13 * s));
    }
}
