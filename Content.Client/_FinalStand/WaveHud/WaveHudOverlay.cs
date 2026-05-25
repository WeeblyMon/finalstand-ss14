using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
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

    // Cache keyed by "AugmentId_level" to avoid reloading on each frame.
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

        // Wave counter widths
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

        // Enemy counter metrics (computed once; used for both stacking and drawing)
        _enemyFont ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 16);

        var showEnemies = EnemiesTotal > 0;
        var counterStr  = $"{EnemiesAlive} / {EnemiesTotal}";
        var counterDims = showEnemies
            ? screen.GetDimensions(_enemyFont, counterStr, 1f)
            : Vector2.Zero;

        // Stack upward from wave digits: enemy counter → augment icons
        var enemyRowTop   = digitY - stackGap - counterDims.Y;
        var iconRowBottom  = showEnemies ? enemyRowTop - stackGap : digitY - stackGap;
        var iconRowTop     = iconRowBottom - iconSize;

        // Active augment slot icons
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

        // Enemy counter
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

        // Wave digit textures
        var x = digitStartX;
        var y = digitY;
        for (var i = 0; i < waveStr.Length; i++)
        {
            var tex = _digits[waveStr[i] - '0'];
            screen.DrawTextureRect(tex, new UIBox2(x, y, x + widths[i], y + digitHeight));
            x += widths[i];
        }

        // Credits — bottom-left
        _creditFont ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 28);

        screen.DrawString(_creditFont, new Vector2(margin, screenSize.Y - 220f),
            $"${CurrentCredits:N0}", Color.Gold);
    }

    private Texture? GetCachedIcon(string augmentId)
    {
        AugmentLevels.TryGetValue(augmentId, out var level);
        var key = $"{augmentId}_{level}";

        if (_iconCache.TryGetValue(key, out var cached))
            return cached;

        var name = augmentId.ToLowerInvariant();
        Texture? tex = null;

        if (_resourceCache.TryGetResource<TextureResource>(
                new ResPath($"/Textures/_FinalStand/Interface/Augments/{name}level{level}.png"), out var res))
            tex = res!.Texture;
        else if (_resourceCache.TryGetResource<TextureResource>(
                new ResPath($"/Textures/_FinalStand/Interface/Augments/{name}level0.png"), out res))
            tex = res!.Texture;

        _iconCache[key] = tex;
        return tex;
    }
}
