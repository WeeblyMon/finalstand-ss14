using System.Numerics;
using Content.Shared._FinalStand.Augments;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.WaveHud;

public sealed class WaveHudOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IInputManager _input = default!;

    private Font? _labelFont;
    private Font? _valueFont;
    private Font? _tooltipNameFont;
    private Font? _tooltipBodyFont;
    private int _cachedLabelPt = -1;
    private int _cachedValuePt = -1;

    private Texture? _iconCredits;
    private Texture? _iconTimer;
    private Texture? _iconEnemies;
    private Texture? _iconWave;
    private bool _hudIconsLoaded;

    public int CurrentWave    = 1;
    public int CurrentCredits = 0;
    public int EnemiesAlive   = 0;
    public int EnemiesTotal   = 0;
    public string[] ActiveSlots  = Array.Empty<string>();
    public Dictionary<string, int> AugmentLevels = new();
    public float PrepSecondsRemaining = -1f;
    public bool IsPrepPhase = false;

    private static readonly TextureLoadParameters LinearParams = new()
    {
        SampleParameters = new TextureSampleParameters { Filter = true },
    };

    private readonly Dictionary<string, Texture?> _augIconCache = new();
    // rebuilt each frame: augment cell bounds + id for hover detection
    private readonly List<(UIBox2 Cell, string Id)> _augCells = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public WaveHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    private void EnsureHudIcons()
    {
        if (_hudIconsLoaded)
            return;
        _hudIconsLoaded = true;
        _iconCredits = LoadHudIcon("hud_credits");
        _iconTimer   = LoadHudIcon("hud_timer");
        _iconEnemies = LoadHudIcon("hud_enemies");
        _iconWave    = LoadHudIcon("hud_wave");
    }

    private Texture? LoadHudIcon(string name)
    {
        try
        {
            return _resourceCache
                .GetResource<TextureResource>(new ResPath($"/Textures/_FinalStand/Interface/HUD/{name}.png"))
                .Texture;
        }
        catch { return null; }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        const float refWidth = 1920f;
        const float margin = 24f;

        var screen = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var s = Math.Clamp(screenSize.X / refWidth, 0.45f, 1.0f);

        var iconSz = MathF.Round(32f * s);
        var iconGap = MathF.Round(9f * s);
        var rowPad = MathF.Round(6f * s);
        const float sepH = 1f;
        var augIconSz = MathF.Round(29f * s);
        var augGap = MathF.Round(3f * s);
        var panelW = MathF.Round(205f * s);

        var labelPt = Math.Max(6, (int)MathF.Round(8f * s));
        var valuePt = Math.Max(10, (int)MathF.Round(20f * s));

        EnsureHudIcons();

        var notoRes = _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf"));
        if (_cachedLabelPt != labelPt) { _labelFont = new VectorFont(notoRes, labelPt); _cachedLabelPt = labelPt; }
        if (_cachedValuePt != valuePt) { _valueFont = new VectorFont(notoRes, valuePt); _cachedValuePt = valuePt; }
        _tooltipNameFont ??= new VectorFont(notoRes, 13);
        _tooltipBodyFont ??= new VectorFont(notoRes, 11);

        var labelH = screen.GetDimensions(_labelFont!, "ENEMIES LEFT", 1f).Y;
        var valueH = screen.GetDimensions(_valueFont!, "$888,888", 1f).Y;
        var rowContentH = Math.Max(iconSz, labelH + 4f + valueH);
        var rowH = rowContentH + rowPad * 2f;

        var sepColor = new Color(0.23f, 0.26f, 0.32f, 0.8f);
        var muted = Color.FromHex("#8FA1B3");

        var totalH = sepH;
        totalH += sepH + rowH;
        if (IsPrepPhase && PrepSecondsRemaining >= 0f) totalH += sepH + rowH;
        totalH += sepH + rowPad + labelH + 4f + augIconSz + rowPad;
        if (EnemiesTotal > 0) totalH += sepH + rowH;
        totalH += sepH + rowH;

        var panelX = screenSize.X - margin - panelW;
        float y = screenSize.Y - margin - totalH;

        float DrawRow(Texture? icon, string label, string value, Color valueColor)
        {
            screen.DrawRect(new UIBox2(panelX, y, panelX + panelW, y + sepH), sepColor);
            var innerY = y + sepH + rowPad;
            var iconY = innerY + (rowContentH - iconSz) / 2f;
            var iconBox = new UIBox2(panelX, iconY, panelX + iconSz, iconY + iconSz);
            if (icon != null) screen.DrawTextureRect(icon, iconBox);
            var textX = panelX + iconSz + iconGap;
            var textBlockH = labelH + 4f + valueH;
            var textStartY = innerY + (rowContentH - textBlockH) / 2f;
            screen.DrawString(_labelFont!, new Vector2(textX, textStartY), label, muted);
            screen.DrawString(_valueFont!, new Vector2(textX, textStartY + labelH + 4f), value, valueColor);
            return y + sepH + rowH;
        }

        y = DrawRow(_iconCredits, "CREDITS", $"${CurrentCredits:N0}", Color.White);

        if (IsPrepPhase && PrepSecondsRemaining >= 0f)
        {
            var secs = (int)MathF.Ceiling(PrepSecondsRemaining);
            y = DrawRow(_iconTimer, "TIMER", $"{secs / 60}:{secs % 60:D2}", Color.FromHex("#e2b662"));
        }

        // ── Augments ─────────────────────────────────────────────────────────
        screen.DrawRect(new UIBox2(panelX, y, panelX + panelW, y + sepH), sepColor);
        var augLabelY = y + sepH + rowPad;
        screen.DrawString(_labelFont!, new Vector2(panelX, augLabelY), "AUGMENTS", muted);
        var augIconsY = augLabelY + labelH + 4f;

        _augCells.Clear();
        var ix = panelX;
        foreach (var id in ActiveSlots)
        {
            var cell = new UIBox2(ix, augIconsY, ix + augIconSz, augIconsY + augIconSz);
            _augCells.Add((cell, id));
            screen.DrawRect(cell, Color.FromHex("#1A1D23"));
            if (!string.IsNullOrEmpty(id))
            {
                var tex = GetAugmentIcon(id);
                if (tex != null) screen.DrawTextureRect(tex, cell);
            }
            ix += augIconSz + augGap;
        }
        y += sepH + rowPad + labelH + 4f + augIconSz + rowPad;

        if (EnemiesTotal > 0)
            y = DrawRow(_iconEnemies, "ENEMIES LEFT", $"{EnemiesAlive}", Color.FromHex("#d1292c"));

        y = DrawRow(_iconWave, "WAVE", $"{CurrentWave:D2}", Color.FromHex("#d1292c"));
        screen.DrawRect(new UIBox2(panelX, y, panelX + panelW, y + sepH), sepColor);

        // ── Augment tooltip ───────────────────────────────────────────────────
        var mouse = _input.MouseScreenPosition.Position;
        foreach (var (cell, id) in _augCells)
        {
            if (string.IsNullOrEmpty(id) || !cell.Contains(mouse))
                continue;
            if (!FSAugmentDef.All.TryGetValue(id, out var def))
                break;

            var level = AugmentLevels.GetValueOrDefault(id, 0);
            var levelText = $"Level {level} / {FSAugmentDef.MaxLevel}";
            var effectText = level > 0 ? def.LevelEffects[level - 1] : "Not yet upgraded.";

            const float tipPad = 8f;
            const float tipW = 200f;

            var nameDims = screen.GetDimensions(_tooltipNameFont!, def.Name, 1f);
            var levelDims = screen.GetDimensions(_tooltipBodyFont!, levelText, 1f);
            var effectDims = screen.GetDimensions(_tooltipBodyFont!, effectText, 1f);

            var tipH = tipPad * 2f + nameDims.Y + 4f + levelDims.Y + 4f + effectDims.Y;

            // position above the hovered cell, right-aligned with panel
            var tipX = panelX + panelW - tipW;
            var tipY = cell.Top - tipH - 6f;
            if (tipY < 0f) tipY = cell.Bottom + 6f;

            var tipBox = new UIBox2(tipX, tipY, tipX + tipW, tipY + tipH);
            screen.DrawRect(tipBox, Color.FromHex("#0D0F12"));
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Top, tipBox.Right, tipBox.Top + 1f), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Bottom - 1f, tipBox.Right, tipBox.Bottom), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Top, tipBox.Left + 1f, tipBox.Bottom), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Right - 1f, tipBox.Top, tipBox.Right, tipBox.Bottom), sepColor);

            var tx = tipX + tipPad;
            var ty = tipY + tipPad;
            screen.DrawString(_tooltipNameFont!, new Vector2(tx, ty), def.Name, Color.White);
            ty += nameDims.Y + 4f;
            screen.DrawString(_tooltipBodyFont!, new Vector2(tx, ty), levelText, Color.FromHex("#e2b662"));
            ty += levelDims.Y + 4f;
            screen.DrawString(_tooltipBodyFont!, new Vector2(tx, ty), effectText, muted);
            break;
        }
    }

    private Texture? GetAugmentIcon(string augmentId)
    {
        if (_augIconCache.TryGetValue(augmentId, out var cached))
            return cached;
        if (!FSAugmentDef.All.TryGetValue(augmentId, out var def))
        {
            _augIconCache[augmentId] = null;
            return null;
        }
        var file = def.IconFile ?? def.Id.ToLowerInvariant();
        var path = $"/Textures/_FinalStand/Interface/Augments/Icons/{file}.png";
        Texture? tex = null;
        if (_resourceCache.TryContentFileRead(path, out var stream))
            using (stream)
                tex = _clyde.LoadTextureFromPNGStream(stream, path, LinearParams);
        _augIconCache[augmentId] = tex;
        return tex;
    }
}
