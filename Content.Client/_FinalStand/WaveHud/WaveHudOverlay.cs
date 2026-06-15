using System.Numerics;
using Content.Shared._FinalStand.Augments;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.WaveHud;

public sealed class WaveHudOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly Texture[] _digits;
    private Font? _creditFont;
    private Font? _enemyFont;

    public int CurrentWave    = 1;
    public int CurrentCredits = 0;
    public int EnemiesAlive   = 0;
    public int EnemiesTotal   = 0;
    public string[] ActiveSlots  = Array.Empty<string>();
    public Dictionary<string, int> AugmentLevels = new();

    private static readonly TextureLoadParameters LinearParams = new()
    {
        SampleParameters = new TextureSampleParameters { Filter = true },
    };

    private readonly Dictionary<string, Texture?> _iconCache = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public WaveHudOverlay(Texture[] digits)
    {
        IoCManager.InjectDependencies(this);
        _digits = digits;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        const float digitHeight = 80f;
        const float margin      = 24f;
        const float iconSize    = 28f;
        const float iconGap     = 3f;
        const float stackGap    = 6f;

        var screen     = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var waveStr    = CurrentWave.ToString();
        var widths     = new float[waveStr.Length];
        var totalWidth = 0f;
        for (var i = 0; i < waveStr.Length; i++)
        {
            var tex   = _digits[waveStr[i] - '0'];
            widths[i]  = tex.Width * (digitHeight / tex.Height);
            totalWidth += widths[i];
        }

        var digitStartX = screenSize.X - margin - totalWidth;
        var digitY      = screenSize.Y - margin - digitHeight;
        var colCenterX  = digitStartX + totalWidth / 2f;

        _enemyFont ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 16);

        var showEnemies = EnemiesTotal > 0;
        var counterStr  = $"{EnemiesAlive} / {EnemiesTotal}";
        var counterDims = showEnemies
            ? screen.GetDimensions(_enemyFont, counterStr, 1f)
            : Vector2.Zero;

        var enemyRowTop   = digitY - stackGap - counterDims.Y;
        var iconRowBottom  = showEnemies ? enemyRowTop - stackGap : digitY - stackGap;
        var iconRowTop     = iconRowBottom - iconSize;

        var slots = ActiveSlots;
        if (slots.Length > 0)
        {
            var rowWidth = slots.Length * iconSize + (slots.Length - 1) * iconGap;
            var ix       = colCenterX - rowWidth / 2f;

            foreach (var id in slots)
            {
                var cell = new UIBox2(ix, iconRowTop, ix + iconSize, iconRowTop + iconSize);
                screen.DrawRect(cell, new Color(0f, 0f, 0f, 0.45f));

                if (!string.IsNullOrEmpty(id))
                {
                    var tex = GetCachedIcon(id);
                    if (tex != null)
                        screen.DrawTextureRect(tex, cell);
                }

                ix += iconSize + iconGap;
            }
        }

        if (showEnemies)
        {
            var cx   = colCenterX - counterDims.X / 2f;
            var cPos = new Vector2(cx, enemyRowTop);

            const float o     = 1f;
            var black = Color.Black;
            var red   = new Color(1f, 0.22f, 0.22f);
            screen.DrawString(_enemyFont, cPos + new Vector2(-o, -o), counterStr, black);
            screen.DrawString(_enemyFont, cPos + new Vector2( o, -o), counterStr, black);
            screen.DrawString(_enemyFont, cPos + new Vector2(-o,  o), counterStr, black);
            screen.DrawString(_enemyFont, cPos + new Vector2( o,  o), counterStr, black);
            screen.DrawString(_enemyFont, cPos, counterStr, red);
        }

        var x = digitStartX;
        var y = digitY;
        for (var i = 0; i < waveStr.Length; i++)
        {
            var tex = _digits[waveStr[i] - '0'];
            screen.DrawTextureRect(tex, new UIBox2(x, y, x + widths[i], y + digitHeight));
            x += widths[i];
        }

        _creditFont ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 28);

        var creditStr  = $"${CurrentCredits:N0}";
        var creditDims = screen.GetDimensions(_creditFont, creditStr, 1f);
        var creditPos  = new Vector2(screenSize.X - margin - creditDims.X, iconRowTop - stackGap - creditDims.Y);
        screen.DrawString(_creditFont, creditPos, creditStr, Color.Gold);
    }

    private Texture? GetCachedIcon(string augmentId)
    {
        if (_iconCache.TryGetValue(augmentId, out var cached))
            return cached;

        if (!FSAugmentDef.All.TryGetValue(augmentId, out var def))
        {
            _iconCache[augmentId] = null;
            return null;
        }

        var file = def.IconFile ?? def.Id.ToLowerInvariant();
        var path = $"/Textures/_FinalStand/Interface/Augments/Icons/{file}.png";

        Texture? tex = null;
        if (_resourceCache.TryContentFileRead(path, out var stream))
        {
            using (stream)
                tex = _clyde.LoadTextureFromPNGStream(stream, path, LinearParams);
        }

        _iconCache[augmentId] = tex;
        return tex;
    }
}
