using System.Numerics;
using Content.Client._FinalStand.Pings;
using Content.Client._FinalStand.WaveHud;
using Content.Shared._FinalStand.Melee;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Melee;

// Devil May Cry style rank meter under the wave panel: a tilted rank letter that pops in, its title, a draining bar and a hit counter.
public sealed class FSStyleMeterOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private static readonly ResPath RanksRsi = new("/Textures/_FinalStand/Interface/Style/ranks.rsi");
    private static readonly ResPath TitlesRsi = new("/Textures/_FinalStand/Interface/Style/titles.rsi");
    private static readonly ResPath FontPath = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf");

    private static readonly string[] States = ["", "d", "c", "b", "a", "s", "ss", "sss"];

    private static readonly Color[] RankColors =
    [
        Color.White,
        Color.FromHex("#8296b4"),
        Color.FromHex("#3ccdd7"),
        Color.FromHex("#5fe15a"),
        Color.FromHex("#ffcd2d"),
        Color.FromHex("#ff871e"),
        Color.FromHex("#ff372d"),
        Color.FromHex("#ff2d96"),
    ];

    private const float PopTime = 0.28f;
    private const float DropTime = 0.4f;
    private const float FadeSpeed = 4f;
    private static readonly Angle Tilt = Angle.FromDegrees(-6);

    // Layout is drawn for a panel this wide and scaled to the wave panel's real width.
    private const float BaseWidth = 205f;

    private readonly FSStyleMeterSystem _style;
    private readonly Texture?[] _letters = new Texture?[States.Length];
    private readonly Texture?[] _titles = new Texture?[States.Length];
    private Font? _font;
    private int _fontSize;
    private float _alpha;
    private float _lastFrame;
    private int _shownRank;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSStyleMeterOverlay(FSStyleMeterSystem style)
    {
        IoCManager.InjectDependencies(this);
        _style = style;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var now = _style.RealTime;
        var delta = Math.Clamp(now - _lastFrame, 0f, 0.1f);
        _lastFrame = now;

        var rank = _style.Rank;
        var enabled = _cfg.GetCVar(CCVars.FSStyleMeter);
        _alpha = Math.Clamp(_alpha + (enabled && rank > 0 ? delta : -delta) * FadeSpeed, 0f, 1f);
        if (rank > 0)
            _shownRank = rank;

        if (_alpha <= 0f || _shownRank == 0)
            return;

        LoadTextures();
        var handle = args.ScreenHandle;
        var (left, width, top) = Anchor(args.ViewportBounds);
        var s = width / BaseWidth;
        var color = RankColors[_shownRank];
        var since = now - _style.ChangedAt;

        // The letter bursts in big on a rank up and shakes red on a drop.
        var scale = 1f;
        var shake = Vector2.Zero;
        var tint = Color.White;
        if (_style.Rose && since < PopTime)
        {
            var t = 1f - since / PopTime;
            scale += 0.45f * t * t;
        }
        else if (!_style.Rose && since < DropTime)
        {
            var t = 1f - since / DropTime;
            shake = new Vector2(MathF.Sin(now * 90f), MathF.Cos(now * 77f)) * 4f * s * t;
            tint = Color.InterpolateBetween(Color.White, Color.FromHex("#ff4040"), t);
        }

        // The top ranks never quite sit still.
        if (_shownRank >= 6)
            shake += new Vector2(MathF.Sin(now * 37f), MathF.Sin(now * 53f)) * (_shownRank - 5) * 0.8f * s;

        DrawHits(handle, new Vector2(left + 4f * s, top), s, color, now);

        var centre = new Vector2(left + 52f * s, top + 62f * s) + shake;
        var letterSize = new Vector2(92f, 77f) * s;
        var old = handle.GetTransform();

        if (_letters[_shownRank] is { } letter)
        {
            var wobble = Angle.FromDegrees(MathF.Sin(now * 2.3f) * 1.5f);
            handle.SetTransform(Matrix3x2.Multiply(Matrix3Helpers.CreateTransform(centre, Tilt + wobble, new Vector2(scale)), old));
            handle.DrawTextureRect(letter, UIBox2.FromDimensions(-letterSize / 2f, letterSize), tint.WithAlpha(_alpha));
            handle.SetTransform(old);
        }

        // The title slides in from the right after a rank change.
        if (_titles[_shownRank] is { } title)
        {
            var slide = since < 0.25f ? MathF.Pow(1f - since / 0.25f, 2f) * 40f * s : 0f;
            var titleSize = new Vector2(132f, 27.5f) * s;
            var titlePos = new Vector2(left + width - titleSize.X + slide, top + 72f * s);
            handle.DrawTextureRect(title, UIBox2.FromDimensions(titlePos, titleSize), Color.White.WithAlpha(_alpha * (1f - slide / (50f * s))));
        }

        DrawBar(handle, old, new Vector2(left + 8f * s, top + 112f * s), width - 12f * s, s, color);
    }

    // Under the wave panel and anything stacked below it, matching its width.
    private (float Left, float Width, float Top) Anchor(UIBox2i bounds)
    {
        if (!_overlays.TryGetOverlay<WaveHudOverlay>(out var hud) || hud.PanelLeft < 0f || hud.RightColumnBottom < 0f)
            return (bounds.Right - BaseWidth - 24f, BaseWidth, bounds.Top + 260f);

        var top = hud.RightColumnBottom + 8f;
        if (_overlays.TryGetOverlay<FSPingOverlay>(out var pings) && pings.ColumnBottom > top)
            top = pings.ColumnBottom;

        return (hud.PanelLeft, hud.PanelWidth, top + 4f);
    }

    private void DrawBar(DrawingHandleScreen handle, Matrix3x2 old, Vector2 at, float width, float s, Color color)
    {
        var size = new Vector2(width, 6f * s);
        var skew = new Matrix3x2(1f, 0f, -0.6f, 1f, at.X, at.Y);
        handle.SetTransform(Matrix3x2.Multiply(skew, old));

        var border = 1.5f * s;
        handle.DrawRect(new UIBox2(-border, -border, size.X + border, size.Y + border), Color.Black.WithAlpha(0.75f * _alpha));
        handle.DrawRect(new UIBox2(0f, 0f, size.X, size.Y), color.WithAlpha(0.2f * _alpha));

        var fill = _style.Rank == 0 ? 0f : FSStyleComponent.Progress(_style.Score);
        handle.DrawRect(new UIBox2(0f, 0f, size.X * fill, size.Y), color.WithAlpha(_alpha));
        handle.DrawRect(new UIBox2(0f, 0f, size.X * fill, size.Y * 0.35f), Color.White.WithAlpha(0.45f * _alpha));
        handle.SetTransform(old);
    }

    private void DrawHits(DrawingHandleScreen handle, Vector2 at, float s, Color color, float now)
    {
        var hits = _style.Hits;
        if (hits < 3)
            return;

        var size = Math.Max(10, (int) MathF.Round(17f * s));
        if (_font == null || _fontSize != size)
        {
            _font = new VectorFont(_resources.GetResource<FontResource>(FontPath), size);
            _fontSize = size;
        }

        var since = now - _style.HitsChangedAt;
        var pop = since < 0.15f ? 1f + 0.35f * (1f - since / 0.15f) : 1f;
        handle.DrawString(_font, at, $"{hits} HITS", pop, Color.White.WithAlpha(_alpha), new TextOutline(2f * s, color.WithAlpha(_alpha)));
    }

    private void LoadTextures()
    {
        if (_letters[1] != null)
            return;

        var sprites = _entities.System<SpriteSystem>();
        for (var i = 1; i < States.Length; i++)
        {
            _letters[i] = sprites.Frame0(new SpriteSpecifier.Rsi(RanksRsi, States[i]));
            _titles[i] = sprites.Frame0(new SpriteSpecifier.Rsi(TitlesRsi, States[i]));
        }
    }
}
