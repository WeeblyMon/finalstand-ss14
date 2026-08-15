using Content.Shared._FinalStand.WaveHud;
using System.Numerics;
using Content.Client._FinalStand.Shop;
using Content.Client.UserInterface.Screens;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    private static readonly ResPath NotoBoldPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");

    private FontResource? _notoRes;
    private Font? _labelFont;
    private Font? _valueFont;
    private Font? _tooltipNameFont;
    private Font? _tooltipBodyFont;
    private int _cachedLabelPt = -1;
    private int _cachedValuePt = -1;
    private float _cachedLabelH;
    private float _cachedValueH;

    // The hotbar does not move, so the control tree is walked once per screen instead of per frame.
    private Control? _hotbarControl;
    private Control? _hotbarScreen;

    private string _layoutRaw = "";
    private bool _isSeparatedLayout;

    private Texture? _iconCredits;
    private Texture? _iconTimer;
    private Texture? _iconEnemies;
    private Texture? _iconWave;
    private bool _hudIconsLoaded;

    // Text is rebuilt on assignment, not per frame - these change a few times a wave at most.
    private int _currentWave = 1;
    private int _currentCredits;
    private int _enemiesAlive;
    private string _waveText = "01";
    private string _creditsText = "$0";
    private string _enemiesText = "0";

    public int CurrentWave
    {
        get => _currentWave;
        set { if (_currentWave == value) return; _currentWave = value; _waveText = value.ToString("D2"); }
    }

    public int CurrentCredits
    {
        get => _currentCredits;
        set { if (_currentCredits == value) return; _currentCredits = value; _creditsText = $"${value:N0}"; }
    }

    public int EnemiesAlive
    {
        get => _enemiesAlive;
        set { if (_enemiesAlive == value) return; _enemiesAlive = value; _enemiesText = value.ToString(); }
    }

    public int EnemiesTotal   = 0;
    public string[] ActiveSlots  = Array.Empty<string>();
    public Dictionary<string, int> PerkLevels = new();
    public Dictionary<string, int> PerkStacks = new();
    public float PrepSecondsRemaining = -1f;
    public bool IsPrepPhase = false;

    public FSBonusCategory GunDamage;
    public FSBonusCategory FireRate;
    public FSBonusCategory MeleeDamage;
    public FSBonusCategory ExplosiveDamage;
    public FSBonusCategory ReloadSpeed;
    public FSBonusCategory MagazineSize;

    // Bumped when a summary lands; the row list rebuilds on that or on a change of held item.
    private int _bonusVersion;

    public void SetBonusSummary(FSPlayerBonusSummaryEvent ev)
    {
        GunDamage = ev.GunDamage;
        FireRate = ev.FireRate;
        MeleeDamage = ev.MeleeDamage;
        ExplosiveDamage = ev.ExplosiveDamage;
        ReloadSpeed = ev.ReloadSpeed;
        MagazineSize = ev.MagazineSize;
        _bonusVersion++;
    }

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
    // rebuilt each frame: perk cell bounds + id for hover detection
    private readonly List<(UIBox2 Cell, string Id)> _augCells = new();

    // rebuilt each frame: current-bonuses row bounds + label/source tooltip lines for hover detection
    private readonly List<(UIBox2 Cell, string Label, string[] Tooltip)> _bonusRowCells = new();

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

        var notoRes = _notoRes ??= _resourceCache.GetResource<FontResource>(NotoBoldPath);
        if (_cachedLabelPt != labelPt)
        {
            _labelFont = new VectorFont(notoRes, labelPt);
            _cachedLabelPt = labelPt;
            _cachedLabelH = screen.GetDimensions(_labelFont, "ENEMIES LEFT", 1f).Y;
        }
        if (_cachedValuePt != valuePt)
        {
            _valueFont = new VectorFont(notoRes, valuePt);
            _cachedValuePt = valuePt;
            _cachedValueH = screen.GetDimensions(_valueFont, "$888,888", 1f).Y;
        }
        _tooltipNameFont ??= new VectorFont(notoRes, 13);
        _tooltipBodyFont ??= new VectorFont(notoRes, 11);

        var labelH = _cachedLabelH;
        var valueH = _cachedValueH;
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

        var layoutRaw = _cfg.GetCVar(CCVars.UILayout);
        if (layoutRaw != _layoutRaw)
        {
            _layoutRaw = layoutRaw;
            _isSeparatedLayout = Enum.TryParse<ScreenType>(layoutRaw, out var st) && st == ScreenType.Separated;
        }

        var rightEdge = _isSeparatedLayout ? GetViewportPixelWidth() : screenSize.X;
        var panelX = rightEdge - margin - panelW;
        float y = screenSize.Y - margin - totalH;

        PanelLeft = panelX;
        PanelTop = y;
        PanelWidth = panelW;

        DrawBonusIndicator(screen, margin);

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
        y = DrawRow(_iconCredits, "CREDITS", _creditsText, Color.White);

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
            y = DrawRow(_iconEnemies, "ENEMIES LEFT", _enemiesText, Color.FromHex("#d1292c"));

        y = DrawRow(_iconWave, "WAVE", _waveText, Color.FromHex("#d1292c"));
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

        // ── Current-bonuses tooltip ───────────────────────────────────────────
        foreach (var (cell, label, tooltip) in _bonusRowCells)
        {
            if (!cell.Contains(mouse))
                continue;

            const float tipPad = 8f;
            const float tipW = 200f;
            const float lineGap = 3f;

            var headerDims = screen.GetDimensions(_tooltipNameFont!, label, 1f);
            var lineH = screen.GetDimensions(_tooltipBodyFont!, "Ay", 1f).Y;
            var tipH = tipPad * 2f + headerDims.Y + 4f + tooltip.Length * lineH + Math.Max(0, tooltip.Length - 1) * lineGap;

            var tipX = cell.Left;
            var tipY = cell.Top - tipH - 6f;
            if (tipY < 0f) tipY = cell.Bottom + 6f;

            var tipBox = new UIBox2(tipX, tipY, tipX + tipW, tipY + tipH);
            screen.DrawRect(tipBox, Color.FromHex("#0D0F12"));
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Top, tipBox.Right, tipBox.Top + 1f), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Bottom - 1f, tipBox.Right, tipBox.Bottom), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Top, tipBox.Left + 1f, tipBox.Bottom), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Right - 1f, tipBox.Top, tipBox.Right, tipBox.Bottom), sepColor);

            var lx = tipX + tipPad;
            var ly = tipY + tipPad;
            screen.DrawString(_tooltipNameFont!, new Vector2(lx, ly), label, Color.White);
            ly += headerDims.Y + 4f;
            foreach (var line in tooltip)
            {
                screen.DrawString(_tooltipBodyFont!, new Vector2(lx, ly), line, muted);
                ly += lineH + lineGap;
            }
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

    private Font? _tinyFont;
    private readonly Dictionary<string, Texture?> _statIconCache = new();

    private Texture? GetStatIcon(string key)
    {
        if (_statIconCache.TryGetValue(key, out var cached))
            return cached;
        var path = $"/Textures/_FinalStand/Interface/HUD/hud_stat_{key}.png";
        Texture? tex = null;
        if (_resourceCache.TryContentFileRead(path, out var stream))
            using (stream)
                tex = _clyde.LoadTextureFromPNGStream(stream, path, LinearParams);
        _statIconCache[key] = tex;
        return tex;
    }

    private void DrawBonusIndicator(DrawingHandleScreen screen, float margin)
    {
        var rows = BuildVisibleBonusRows();
        _bonusRowCells.Clear();
        if (rows.Count == 0)
            return;

        _tinyFont ??= new VectorFont(_notoRes ??= _resourceCache.GetResource<FontResource>(NotoBoldPath), 11);

        const float rowGap = 4f;
        const float iconTextGap = 4f;
        const float bottomGap = 10f;
        var textH = screen.GetDimensions(_tinyFont, "Ay", 1f).Y;
        var iconSz = textH * 2f;
        var blockH = rows.Count * iconSz + (rows.Count - 1) * rowGap;

        var hotbarTop = FindHotbarTop();
        var blockBottom = (hotbarTop ?? _clyde.ScreenSize.Y - margin) - bottomGap;
        var x = margin;
        var y = blockBottom - blockH;

        foreach (var row in rows)
        {
            var icon = GetStatIcon(row.IconKey);
            if (icon != null)
                screen.DrawTextureRect(icon, new UIBox2(x, y, x + iconSz, y + iconSz), Color.White);

            var textDims = screen.GetDimensions(_tinyFont, row.ValueText, 1f);
            var textPos = new Vector2(x + iconSz + iconTextGap, y + (iconSz - textDims.Y) * 0.5f);
            screen.DrawString(_tinyFont, textPos, row.ValueText, row.ValueColor);

            var cellW = iconSz + iconTextGap + textDims.X;
            _bonusRowCells.Add((new UIBox2(x, y, x + cellW, y + iconSz), row.Label, row.Tooltip));
            y += iconSz + rowGap;
        }
    }

    // Recurses since Hotbar nests at different depths between the two HUD layouts.
    private Control? FindNamedScreenControl(string name)
    {
        var screen = _uiManager.ActiveScreen;
        return screen == null ? null : FindNamedControlRecursive(screen, name, 0);
    }

    private static Control? FindNamedControlRecursive(Control root, string name, int depth)
    {
        if (depth > 5)
            return null;
        foreach (var child in root.Children)
        {
            if (child.Name == name)
                return child;
            var found = FindNamedControlRecursive(child, name, depth + 1);
            if (found != null)
                return found;
        }
        return null;
    }

    private float? FindHotbarTop()
    {
        var screen = _uiManager.ActiveScreen;
        if (screen == null)
        {
            _hotbarScreen = null;
            _hotbarControl = null;
            return null;
        }

        if (!ReferenceEquals(screen, _hotbarScreen) || _hotbarControl is null || _hotbarControl.Disposed)
        {
            _hotbarScreen = screen;
            _hotbarControl = FindNamedControlRecursive(screen, "Hotbar", 0);
        }

        return _hotbarControl == null ? null : _hotbarControl.GlobalPixelRect.Top;
    }

    private readonly record struct BonusRow(string Label, string ValueText, Color ValueColor, string[] Tooltip, string IconKey);

    private FSShopClientSystem? _shop;
    private readonly List<BonusRow> _bonusRows = new();
    private EntityUid? _bonusRowsHeld;
    private int _bonusRowsVersion = -1;

    private static readonly Color BonusPositive = Color.FromHex("#22C55E");
    private static readonly Color BonusNegative = Color.FromHex("#EF4444");

    // Rebuilt on a change of held item or summary, not per frame.
    private List<BonusRow> BuildVisibleBonusRows()
    {
        _shop ??= _entityManager.System<FSShopClientSystem>();
        var held = _shop.GetActiveHeldItem();

        if (_bonusRowsVersion == _bonusVersion && _bonusRowsHeld == held)
            return _bonusRows;

        _bonusRowsVersion = _bonusVersion;
        _bonusRowsHeld = held;
        _bonusRows.Clear();

        var holdingGun = _shop.IsHoldingAnyGun();
        var holdingNonLauncherGun = _shop.IsHoldingNonLauncherGun();
        var holdingExplosive = _shop.IsHoldingExplosive();
        var holdingMelee = _shop.IsHoldingMelee();

        if (holdingNonLauncherGun) AddPctRow(_bonusRows, "Gun", "damage", GunDamage);
        if (holdingGun) AddPctRow(_bonusRows, "Fire Rate", "firerate", FireRate);
        if (holdingMelee) AddPctRow(_bonusRows, "Melee", "melee", MeleeDamage);
        if (holdingExplosive) AddPctRow(_bonusRows, "Explosive", "explosive", ExplosiveDamage);
        if (holdingGun) AddPctRow(_bonusRows, "Reload", "reload", ReloadSpeed);
        if (holdingGun) AddFlatRow(_bonusRows, "Mag Size", "magsize", MagazineSize);

        return _bonusRows;
    }

    private static void AddPctRow(List<BonusRow> rows, string label, string iconKey, FSBonusCategory cat)
    {
        if (MathF.Abs(cat.Percent) < 0.05f)
            return;
        var text = $"{(cat.Percent >= 0 ? "+" : "")}{cat.Percent:0.#}%";
        rows.Add(new BonusRow(label, text, cat.Percent >= 0 ? BonusPositive : BonusNegative, cat.Sources, iconKey));
    }

    private static void AddFlatRow(List<BonusRow> rows, string label, string iconKey, FSBonusCategory cat)
    {
        if (MathF.Abs(cat.Percent) < 0.5f)
            return;
        var n = (int)MathF.Round(cat.Percent);
        var text = n >= 0 ? $"+{n}" : n.ToString();
        rows.Add(new BonusRow(label, text, n >= 0 ? BonusPositive : BonusNegative, cat.Sources, iconKey));
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
