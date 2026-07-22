using System.Numerics;
using Content.Client.UserInterface.Screens;
using Content.Shared._FinalStand.Perks;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.WaveHud;

public sealed class WaveHudOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

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
    public Dictionary<string, int> PerkLevels = new();
    public Dictionary<string, int> PerkStacks = new();
    public float PrepSecondsRemaining = -1f;
    public bool IsPrepPhase = false;

    // Used by WaveHudSystem to position and drive the ready-up overlay section.
    public float PanelLeft = -1f;
    public float PanelTop = -1f;
    public float PanelWidth = 205f;

    public bool IsReadyUpVisible = false;
    public int  ReadyUpCount = 0;
    public int  ReadyUpTotal = 0;
    public bool ReadyUpPlayerIsReady = false;

    // Screen-pixel bounds of the YES and NO buttons; valid only when IsReadyUpVisible.
    public UIBox2 ReadyUpYesBounds = new(-100, -100, -99, -99);
    public UIBox2 ReadyUpNoBounds  = new(-100, -100, -99, -99);

    public event Action<bool>? OnReadyUpClicked;
    private bool _prevClickDown;

    private static readonly TextureLoadParameters LinearParams = new()
    {
        SampleParameters = new TextureSampleParameters { Filter = true },
    };

    private readonly Dictionary<string, Texture?> _augIconCache = new();
    // rebuilt each frame: augment cell bounds + id for hover detection
    private readonly List<(UIBox2 Cell, string Id)> _augCells = new();

    private readonly record struct InterestPopup(string PerkId, int Amount, float Life, float TotalLife);
    private readonly List<InterestPopup> _interestPopups = new();
    private float _creditsRowY;

    public void AddInterestPopup(string PerkId, int amount)
    {
        const float life = 2.5f;
        _interestPopups.Add(new InterestPopup(PerkId, amount, life, life));
    }

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
        var augIconSz = MathF.Round(32f * s);
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

        const float btnPad = 3f;
        var btnH = labelH + btnPad * 2f;

        var totalH = sepH;
        if (IsReadyUpVisible)
            totalH += sepH + rowPad + labelH + 2f + labelH + 3f + btnH;
        totalH += sepH + rowH;
        if (IsPrepPhase && PrepSecondsRemaining >= 0f) totalH += sepH + rowH;
        totalH += sepH + rowPad + labelH + 4f + augIconSz + rowPad;
        if (EnemiesTotal > 0) totalH += sepH + rowH;
        totalH += sepH + rowH;

        var isSeparated = Enum.TryParse<ScreenType>(_cfg.GetCVar(CCVars.UILayout), out var st)
                          && st == ScreenType.Separated;
        var rightEdge = isSeparated ? GetViewportPixelWidth() : screenSize.X;
        var panelX = rightEdge - margin - panelW;
        float y = screenSize.Y - margin - totalH;

        PanelLeft = panelX;
        PanelTop = y;
        PanelWidth = panelW;

        if (IsReadyUpVisible)
        {
            screen.DrawRect(new UIBox2(panelX, y, panelX + panelW, y + sepH), sepColor);
            y += sepH + rowPad;

            screen.DrawString(_labelFont!, new Vector2(panelX, y), "READY UP", muted);
            y += labelH + 2f;

            var countText  = ReadyUpTotal > 0 ? $"{ReadyUpCount} / {ReadyUpTotal} ready" : "—";
            var countColor = ReadyUpCount > 0 ? Color.FromHex("#44FF44") : Color.White;
            screen.DrawString(_labelFont!, new Vector2(panelX, y), countText, countColor);
            y += labelH + 3f;

            var halfW = (panelW - 3f) / 2f;
            var yesBg = ReadyUpPlayerIsReady ? Color.FromHex("#2a6b2a") : Color.FromHex("#1a3d1a");
            var noBg  = !ReadyUpPlayerIsReady && ReadyUpTotal > 0 ? Color.FromHex("#6b2a2a") : Color.FromHex("#3d1a1a");

            ReadyUpYesBounds = new UIBox2(panelX,             y, panelX + halfW,      y + btnH);
            ReadyUpNoBounds  = new UIBox2(panelX + halfW + 3f, y, panelX + panelW,    y + btnH);

            screen.DrawRect(ReadyUpYesBounds, yesBg);
            screen.DrawRect(ReadyUpNoBounds,  noBg);

            var yesDim = screen.GetDimensions(_labelFont!, "YES", 1f);
            var noDim  = screen.GetDimensions(_labelFont!, "NO",  1f);

            screen.DrawString(_labelFont!,
                new Vector2(panelX             + (halfW - yesDim.X) * 0.5f, y + (btnH - yesDim.Y) * 0.5f),
                "YES", Color.FromHex("#44CC44"));
            screen.DrawString(_labelFont!,
                new Vector2(panelX + halfW + 3f + (halfW - noDim.X) * 0.5f, y + (btnH - noDim.Y) * 0.5f),
                "NO", Color.FromHex("#CC4444"));

            y += btnH;
        }

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

        _creditsRowY = y;
        y = DrawRow(_iconCredits, "CREDITS", $"${CurrentCredits:N0}", Color.White);

        // ── Interest popups ───────────────────────────────────────────────────
        for (var pi = 0; pi < _interestPopups.Count; pi++)
        {
            var p = _interestPopups[pi];
            var t = p.Life / p.TotalLife;
            var alpha = MathF.Min(1f, t * 3f); // fade in fast, fade out slow
            var floatOffset = (1f - t) * 40f;  // float upward as it expires

            var popupY = _creditsRowY - floatOffset;
            var amtText = $"+${p.Amount:N0}";
            var amtDim = screen.GetDimensions(_labelFont!, amtText, 1f);

            var popupIconSz = augIconSz * 0.75f;
            var totalW = popupIconSz + 4f + amtDim.X;
            var popupX = panelX - totalW - 8f;

            var tex = GetPerkIcon(p.PerkId);
            if (tex != null)
                screen.DrawTextureRect(tex,
                    new UIBox2(popupX, popupY, popupX + popupIconSz, popupY + popupIconSz),
                    Color.White.WithAlpha(alpha));

            var textPos = new Vector2(popupX + popupIconSz + 4f, popupY + (popupIconSz - amtDim.Y) * 0.5f);
            screen.DrawString(_labelFont!, textPos, amtText, Color.FromHex("#FFD740").WithAlpha(alpha));
        }

        if (IsPrepPhase && PrepSecondsRemaining >= 0f)
        {
            var secs = (int)MathF.Ceiling(PrepSecondsRemaining);
            y = DrawRow(_iconTimer, "TIMER", $"{secs / 60}:{secs % 60:D2}", Color.FromHex("#e2b662"));
        }

        // ── Perks ─────────────────────────────────────────────────────────
        screen.DrawRect(new UIBox2(panelX, y, panelX + panelW, y + sepH), sepColor);
        var augLabelY = y + sepH + rowPad;
        screen.DrawString(_labelFont!, new Vector2(panelX, augLabelY), "PERKS", muted);
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
                var tex = GetPerkIcon(id);
                if (tex != null) screen.DrawTextureRect(tex, cell);

                if (PerkStacks.TryGetValue(id, out var stacks) && stacks > 0)
                {
                    var stackStr = stacks.ToString();
                    var stackDim = screen.GetDimensions(_labelFont!, stackStr, 1f);
                    var tx = cell.Right - stackDim.X - 1f;
                    var ty = cell.Bottom - stackDim.Y;
                    var outline = new Color(0f, 0f, 0f, 0.9f);
                    screen.DrawString(_labelFont!, new Vector2(tx - 1, ty),     stackStr, outline);
                    screen.DrawString(_labelFont!, new Vector2(tx + 1, ty),     stackStr, outline);
                    screen.DrawString(_labelFont!, new Vector2(tx,     ty - 1), stackStr, outline);
                    screen.DrawString(_labelFont!, new Vector2(tx,     ty + 1), stackStr, outline);
                    screen.DrawString(_labelFont!, new Vector2(tx,     ty),     stackStr, Color.FromHex("#FF3333"));
                }
            }
            ix += augIconSz + augGap;
        }
        y += sepH + rowPad + labelH + 4f + augIconSz + rowPad;

        if (EnemiesTotal > 0)
            y = DrawRow(_iconEnemies, "ENEMIES LEFT", $"{EnemiesAlive}", Color.FromHex("#d1292c"));

        y = DrawRow(_iconWave, "WAVE", $"{CurrentWave:D2}", Color.FromHex("#d1292c"));
        screen.DrawRect(new UIBox2(panelX, y, panelX + panelW, y + sepH), sepColor);

        // ── Perk tooltip ───────────────────────────────────────────────────
        var mouse = _input.MouseScreenPosition.Position;
        foreach (var (cell, id) in _augCells)
        {
            if (string.IsNullOrEmpty(id) || !cell.Contains(mouse))
                continue;
            if (!FSPerkDef.All.TryGetValue(id, out var def))
                break;

            var level = PerkLevels.GetValueOrDefault(id, 0);
            var levelText = $"Level {level} / {FSPerkDef.MaxLevel}";
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

    protected override void FrameUpdate(FrameEventArgs args)
    {
        for (var i = _interestPopups.Count - 1; i >= 0; i--)
        {
            var p = _interestPopups[i];
            var updated = p with { Life = p.Life - args.DeltaSeconds };
            if (updated.Life <= 0f)
                _interestPopups.RemoveAt(i);
            else
                _interestPopups[i] = updated;
        }

        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        if (IsReadyUpVisible && down && !_prevClickDown)
        {
            var pos = _input.MouseScreenPosition.Position;
            if (ReadyUpYesBounds.Contains(pos))
                OnReadyUpClicked?.Invoke(true);
            else if (ReadyUpNoBounds.Contains(pos))
                OnReadyUpClicked?.Invoke(false);
        }
        _prevClickDown = down;
    }

    private float GetViewportPixelWidth()
    {
        var screen = _uiManager.ActiveScreen;
        if (screen != null)
        {
            foreach (var child in screen.Children)
            {
                if (child is SplitContainer split)
                {
                    foreach (var sc in split.Children)
                    {
                        if (sc.Name == "ViewportContainer")
                            return sc.PixelSize.X;
                    }
                }
            }
        }
        return _clyde.ScreenSize.X - 300f;
    }

    private Texture? GetPerkIcon(string perkId)
    {
        if (_augIconCache.TryGetValue(perkId, out var cached))
            return cached;
        if (!FSPerkDef.All.TryGetValue(perkId, out var def))
        {
            _augIconCache[perkId] = null;
            return null;
        }
        var file = def.IconFile ?? def.Id.ToLowerInvariant();
        var path = $"/Textures/_FinalStand/Interface/Perks/Icons/{file}.png";
        Texture? tex = null;
        if (_resourceCache.TryContentFileRead(path, out var stream))
            using (stream)
                tex = _clyde.LoadTextureFromPNGStream(stream, path, LinearParams);
        _augIconCache[perkId] = tex;
        return tex;
    }
}
