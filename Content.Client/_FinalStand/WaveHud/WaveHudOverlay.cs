using Content.Client._FinalStand.Interface;
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
    private Font? _midFont;
    private Font? _tooltipNameFont;
    private Font? _tooltipBodyFont;
    private Font? _promptFont;
    private Font? _promptSubFont;
    private int _cachedLabelPt = -1;
    private int _cachedValuePt = -1;
    private int _cachedPromptPt = -1;
    private float _cachedLabelH;
    private float _cachedValueH;

    private Control? _hotbarControl;
    private Control? _hotbarScreen;
    private Control? _alertsControl;
    private Control? _alertsScreen;

    private float panelW0 = 150f;

    private string _layoutRaw = "";
    private bool _isSeparatedLayout;

    private Texture? _iconCredits;
    private Texture? _iconTimer;
    private Texture? _iconEnemies;
    private Texture? _iconWave;
    private bool _hudIconsLoaded;

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

    public bool IsDarkWave = false;
    public float DarkWaveSecondsRemaining;

    public FSBonusCategory GunDamage;
    public FSBonusCategory FireRate;
    public FSBonusCategory MeleeDamage;
    public FSBonusCategory ExplosiveDamage;
    public FSBonusCategory ReloadSpeed;
    public FSBonusCategory MagazineSize;

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

    public float PanelLeft = -1f;
    public float PanelTop = -1f;
    public float PanelWidth = 205f;

    public bool IsReadyUpVisible = false;
    public int  ReadyUpCount = 0;
    public int  ReadyUpTotal = 0;
    public bool ReadyUpPlayerIsReady = false;

    public UIBox2 ReadyUpYesBounds = new(-100, -100, -99, -99);
    public UIBox2 ReadyUpNoBounds  = new(-100, -100, -99, -99);

    public bool IsRespawnOfferVisible = false;
    public int  RespawnCost = 0;

    public string? CasualtyStatus;
    public bool CasualtyResponded;

    public readonly record struct MedicalBuffRow(string Name, string IconKey, int SecondsRemaining, string Source);

    public readonly List<MedicalBuffRow> MedicalBuffs = new();

    public int MedicalFund;

    public float DirectiveFlash;
    public Color DirectiveFlashColour = FSPalette.Ok;

    public int MedicalFundUnused;
    public bool ShowMedicalFund;

    public string? HarvestStatus;
    public bool HarvestCapped;
    public float HarvestStock;
    public float HarvestAccrued;
    public float HarvestCap;

    public UIBox2 RespawnButtonBounds = new(-100, -100, -99, -99);

    public event Action? OnRespawnClicked;
    private float _respawnClickCooldown;

    public event Action<bool>? OnReadyUpClicked;
    private bool _prevClickDown;

    private static readonly TextureLoadParameters LinearParams = new()
    {
        SampleParameters = new TextureSampleParameters { Filter = true },
    };

    private readonly Dictionary<string, Texture?> _augIconCache = new();
    private readonly List<(UIBox2 Cell, string Id)> _augCells = new();

    private readonly List<(UIBox2 Cell, string Label, string[] Tooltip)> _bonusRowCells = new();

    private readonly record struct InterestPopup(string PerkId, int Amount, float Life, float TotalLife);
    private readonly List<InterestPopup> _interestPopups = new();
    private float _creditsRowY;

    public void AddInterestPopup(string PerkId, int amount)
    {
        const float life = 2.5f;
        _interestPopups.Add(new InterestPopup(PerkId, amount, life, life));
    }

    public void AddHealPayout(int amount, bool diminished)
    {
        const float life = 1.6f;
        _healPayouts.Add(new HealPayout(amount, diminished, life, life));
    }

    private readonly record struct HealPayout(int Amount, bool Diminished, float Life, float TotalLife);
    private readonly List<HealPayout> _healPayouts = new();

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

        const float rowIconSz = 20f;
        const float augIconSz = 32f;

        var iconGap = MathF.Round(9f * s);
        var rowPad = MathF.Round(6f * s);
        const float sepH = 1f;
        var augGap = MathF.Round(3f * s);
        var panelW = MathF.Round(RightColumnWidth * s);

        const int labelPt = 11;
        var valuePt = Math.Max(14, (int)MathF.Round(20f * s));

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
            _midFont = new VectorFont(notoRes, Math.Max(12, (int) MathF.Round(15f * s)));
            _cachedValuePt = valuePt;
            _cachedValueH = screen.GetDimensions(_valueFont, "$888,888", 1f).Y;
        }
        _tooltipNameFont ??= new VectorFont(notoRes, 13);
        _tooltipBodyFont ??= new VectorFont(notoRes, 11);

        var promptPt = Math.Max(14, (int)MathF.Round(28f * s));
        if (_cachedPromptPt != promptPt)
        {
            _promptFont = new VectorFont(notoRes, promptPt);
            _promptSubFont = new VectorFont(notoRes, Math.Max(9, (int)MathF.Round(14f * s)));
            _cachedPromptPt = promptPt;
        }

        var labelH = _cachedLabelH;
        var valueH = _cachedValueH;
        var rowContentH = Math.Max(rowIconSz, labelH + 4f + valueH);
        var rowH = rowContentH + rowPad * 2f;

        var sepColor = FSPalette.PanelEdge;
        var muted = FSPalette.TextMuted;

        const float btnPad = 3f;
        var btnH = labelH + btnPad * 2f;

        var totalH = sepH;
        if (IsReadyUpVisible)
            totalH += sepH + rowPad + labelH + 2f + labelH + 3f + btnH + rowPad;
        if (IsPrepPhase && PrepSecondsRemaining >= 0f) totalH += sepH + rowH;
        if (IsDarkWave || EnemiesTotal > 0) totalH += sepH + rowH;
        totalH += sepH + rowH;

        var layoutRaw = _cfg.GetCVar(CCVars.UILayout);
        if (layoutRaw != _layoutRaw)
        {
            _layoutRaw = layoutRaw;
            _isSeparatedLayout = Enum.TryParse<ScreenType>(layoutRaw, out var st) && st == ScreenType.Separated;
        }

        var rightEdge = _isSeparatedLayout ? GetViewportPixelWidth() : screenSize.X;
        var panelX = rightEdge - margin - panelW;
        var panelInset = 8f;

        float y = margin;

        PanelLeft = panelX;
        PanelTop = y;
        PanelWidth = panelW;

        panelW0 = panelW;

        var waveBox = new UIBox2(panelX, y, panelX + panelW, y + totalH);
        DrawPanel(screen, waveBox, VitalsBack, VitalsEdge, FSPalette.Danger);

        DrawBonusIndicator(screen, margin);
        DrawVitals(screen, margin, BottomBandLift);

        var weaponTop = DrawWeaponModuleAndGetTop(screen, rightEdge - margin, BottomBandLift);
        var throwH = DrawThrowables(screen, rightEdge - margin, weaponTop - 6f);
        RightStackHeight = screenSize.Y - BottomBandLift - (weaponTop - 6f - throwH);

        if (IsRespawnOfferVisible)
        {
            const string headline = "YOU ARE DOWN";
            var subline = $"Wait for a medic, or revive for ${RespawnCost:N0}";

            var headDims = screen.GetDimensions(_promptFont!, headline, 1f);
            var subDims = screen.GetDimensions(_promptSubFont!, subline, 1f);

            var promptY = screenSize.Y * 0.30f;
            var gap = MathF.Round(6f * s);

            DrawVignette(screen, screenSize);

            DrawShadowed(screen, _promptFont!,
                new Vector2((screenSize.X - headDims.X) * 0.5f, promptY),
                headline, FSPalette.DangerSoft);

            DrawShadowed(screen, _promptSubFont!,
                new Vector2((screenSize.X - subDims.X) * 0.5f, promptY + headDims.Y + gap),
                subline, FSPalette.TextBright);

            var afterTextY = promptY + headDims.Y + gap + subDims.Y;

            if (CasualtyStatus is { } casualty)
            {
                var casualtyDims = screen.GetDimensions(_promptSubFont!, casualty, 1f);
                var casualtyColor = CasualtyResponded ? FSPalette.Ok : FSPalette.TextMuted;

                DrawShadowed(screen, _promptSubFont!,
                    new Vector2((screenSize.X - casualtyDims.X) * 0.5f, afterTextY + gap * 0.5f),
                    casualty, casualtyColor);

                afterTextY += gap * 0.5f + casualtyDims.Y;
            }

            const string btnLabel = "REVIVE NOW";
            var btnDim = screen.GetDimensions(_labelFont!, btnLabel, 1f);
            var btnPadX = MathF.Round(20f * s);
            var btnPadY = MathF.Round(8f * s);
            var respawnBtnW = btnDim.X + btnPadX * 2f;
            var respawnBtnH = btnDim.Y + btnPadY * 2f;
            var btnX = (screenSize.X - respawnBtnW) * 0.5f;
            var btnY = afterTextY + gap * 2f;

            RespawnButtonBounds = new UIBox2(btnX, btnY, btnX + respawnBtnW, btnY + respawnBtnH);
            DrawPanel(screen, RespawnButtonBounds, VitalsBack, VitalsEdge, FSPalette.Danger);
            DrawShadowed(screen,
                _labelFont!,
                new Vector2(btnX + btnPadX, btnY + btnPadY),
                btnLabel,
                FSPalette.Danger);
        }
        else
        {
            RespawnButtonBounds = new UIBox2(-100, -100, -99, -99);
        }

        void DrawReadyUpBlock()
        {
            if (!IsReadyUpVisible)
                return;

            screen.DrawRect(new UIBox2(panelX + panelInset, y, panelX + panelW - panelInset, y + sepH), sepColor);
            y += sepH + rowPad;

            screen.DrawString(_labelFont!, new Vector2(panelX + panelInset, y), "READY UP", muted);
            y += labelH + 2f;

            var countText  = ReadyUpTotal > 0 ? $"{ReadyUpCount} / {ReadyUpTotal} ready" : "—";
            var countColor = ReadyUpCount > 0 ? FSPalette.Ok : Color.White;
            screen.DrawString(_labelFont!, new Vector2(panelX + panelInset, y), countText, countColor);
            y += labelH + 3f;

            var btnRowW = panelW - panelInset * 2f;
            var halfW = (btnRowW - 3f) / 2f;
            var yesBg = ReadyUpPlayerIsReady ? FSPalette.Ok.WithAlpha(0.35f) : FSPalette.CellBack;
            var noBg  = !ReadyUpPlayerIsReady && ReadyUpTotal > 0 ? FSPalette.Danger.WithAlpha(0.35f) : FSPalette.CellBack;

            var btnX = panelX + panelInset;
            ReadyUpYesBounds = new UIBox2(btnX,             y, btnX + halfW,   y + btnH);
            ReadyUpNoBounds  = new UIBox2(btnX + halfW + 3f, y, btnX + btnRowW, y + btnH);

            DrawRounded(screen, ReadyUpYesBounds, yesBg, 2f);
            DrawRounded(screen, ReadyUpNoBounds,  noBg,  2f);

            var yesDim = screen.GetDimensions(_labelFont!, "YES", 1f);
            var noDim  = screen.GetDimensions(_labelFont!, "NO",  1f);

            screen.DrawString(_labelFont!,
                new Vector2(btnX + (halfW - yesDim.X) * 0.5f, y + (btnH - yesDim.Y) * 0.5f),
                "YES", FSPalette.Ok);
            screen.DrawString(_labelFont!,
                new Vector2(btnX + halfW + 3f + (halfW - noDim.X) * 0.5f, y + (btnH - noDim.Y) * 0.5f),
                "NO", FSPalette.Danger);

            y += btnH + rowPad;
        }

        float DrawRow(Texture? icon, string label, string value, Color valueColor, Font? valueFont = null)
        {
            screen.DrawRect(new UIBox2(panelX + panelInset, y, panelX + panelW - panelInset, y + sepH), sepColor);
            var innerY = y + sepH + rowPad;
            var iconY = innerY + (rowContentH - rowIconSz) / 2f;
            var iconBox = new UIBox2(panelX + panelInset, iconY,
                panelX + panelInset + rowIconSz, iconY + rowIconSz);
            if (icon != null)
                screen.DrawTextureRect(icon, iconBox, Color.White.WithAlpha(0.55f));

            var font = valueFont ?? _valueFont!;
            var thisValueH = screen.GetDimensions(font, value, 1f).Y;
            var textX = panelX + panelInset + rowIconSz + iconGap;
            var textBlockH = labelH + 4f + thisValueH;
            var textStartY = innerY + (rowContentH - textBlockH) / 2f;
            screen.DrawString(_labelFont!, new Vector2(textX, textStartY), label, muted);
            screen.DrawString(font, new Vector2(textX, textStartY + labelH + 4f), value, valueColor);
            return y + sepH + rowH;
        }

        y = DrawRow(_iconWave, "WAVE", _waveText, FSPalette.Danger);

        if (IsDarkWave)
        {
            var secs = Math.Max(0f, DarkWaveSecondsRemaining);
            var survive = $"{(int) (secs / 60f):D1}:{(int) (secs % 60f):D2}";
            y = DrawRow(_iconEnemies, "SURVIVE", survive, FSPalette.Warn, _midFont);
        }
        else if (EnemiesTotal > 0)
        {
            y = DrawRow(_iconEnemies, "ENEMIES LEFT", _enemiesText, FSPalette.DangerSoft, _midFont);
        }

        if (IsPrepPhase && PrepSecondsRemaining >= 0f)
        {
            var secs = (int)MathF.Ceiling(PrepSecondsRemaining);
            y = DrawRow(_iconTimer, "NEXT", $"{secs / 60}:{secs % 60:D2}", FSPalette.Warn, _midFont);
        }

        DrawReadyUpBlock();
        screen.DrawRect(new UIBox2(panelX + panelInset, y, panelX + panelW - panelInset, y + sepH), sepColor);

        DrawTriage(screen, panelX, y + sepH + 10f);

        var topLeftTop = _isSeparatedLayout ? TopLeftYSeparated : TopLeftY;
        var leftX = TopLeftX + TopLeftPad;
        var leftY = topLeftTop;

        DrawShadowed(screen, _labelFont!, new Vector2(leftX, leftY), "PERKS", muted);
        var augIconsY = leftY + labelH + 4f;

        _augCells.Clear();
        var ix = leftX;
        foreach (var id in ActiveSlots)
        {
            var cell = new UIBox2(ix, augIconsY, ix + augIconSz, augIconsY + augIconSz);
            _augCells.Add((cell, id));

            if (string.IsNullOrEmpty(id))
            {
                screen.DrawRect(cell, FSPalette.CellEdge, filled: false);
            }
            else
            {
                DrawRounded(screen, cell, FSPalette.CellBack);
                screen.DrawRect(cell, FSPalette.PanelEdge, filled: false);
            }
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
                    screen.DrawString(_labelFont!, new Vector2(tx,     ty),     stackStr, FSPalette.Danger);
                }
            }
            ix += augIconSz + augGap;
        }

        _creditsRowY = augIconsY + augIconSz + 6f;
        DrawShadowed(screen, _valueFont!, new Vector2(leftX, _creditsRowY), _creditsText,
            FSPalette.Money);

        if (ShowMedicalFund)
        {
            var fundText = Loc.GetString("fs-hud-medical-fund", ("amount", MedicalFund));
            DrawShadowed(screen, _labelFont!,
                new Vector2(leftX, _creditsRowY + screen.GetDimensions(_valueFont!, _creditsText, 1f).Y + 2f),
                fundText, FSPalette.Ok);
        }

        var creditsW = screen.GetDimensions(_valueFont!, _creditsText, 1f).X;
        for (var pi = 0; pi < _interestPopups.Count; pi++)
        {
            var p = _interestPopups[pi];
            var t = p.Life / p.TotalLife;
            var alpha = MathF.Min(1f, t * 3f);
            var floatOffset = (1f - t) * 40f;

            var popupY = _creditsRowY - floatOffset;
            var amtText = $"+${p.Amount:N0}";
            var amtDim = screen.GetDimensions(_labelFont!, amtText, 1f);

            var popupIconSz = augIconSz * 0.75f;
            var popupX = leftX + creditsW + 10f;

            var tex = GetPerkIcon(p.PerkId);
            if (tex != null)
                screen.DrawTextureRect(tex,
                    new UIBox2(popupX, popupY, popupX + popupIconSz, popupY + popupIconSz),
                    Color.White.WithAlpha(alpha));

            var textPos = new Vector2(popupX + popupIconSz + 4f, popupY + (popupIconSz - amtDim.Y) * 0.5f);
            screen.DrawString(_labelFont!, textPos, amtText, FSPalette.Money.WithAlpha(alpha));
        }

        if (DirectiveFlash > 0f)
        {
            var wash = DirectiveFlashColour.WithAlpha(MathF.Min(0.30f, DirectiveFlash * 0.30f));
            const int frames = 22;

            for (var i = 0; i < frames; i++)
            {
                var inset = i * 3f;
                var a = wash.WithAlpha(wash.A * (1f - i / (float) frames));

                screen.DrawRect(new UIBox2(inset, inset, screenSize.X - inset, inset + 3f), a);
                screen.DrawRect(new UIBox2(inset, screenSize.Y - inset - 3f, screenSize.X - inset, screenSize.Y - inset), a);
                screen.DrawRect(new UIBox2(inset, inset, inset + 3f, screenSize.Y - inset), a);
                screen.DrawRect(new UIBox2(screenSize.X - inset - 3f, inset, screenSize.X - inset, screenSize.Y - inset), a);
            }
        }

        for (var hi = 0; hi < _healPayouts.Count; hi++)
        {
            var h = _healPayouts[hi];
            var ht = h.Life / h.TotalLife;
            var halpha = MathF.Min(1f, ht * 4f);
            var text = $"+${h.Amount:N0}";
            var colour = h.Diminished ? FSPalette.TextDim : FSPalette.Ok;

            screen.DrawString(_labelFont!,
                new Vector2(leftX + creditsW + 10f, _creditsRowY - (1f - ht) * 26f),
                text, colour.WithAlpha(halpha));
        }

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

            var tipX = Math.Clamp(cell.Left, 0f, MathF.Max(0f, _clyde.ScreenSize.X - tipW));
            var tipY = cell.Top - tipH - 6f;
            if (tipY < 0f) tipY = cell.Bottom + 6f;

            var tipBox = new UIBox2(tipX, tipY, tipX + tipW, tipY + tipH);
            screen.DrawRect(tipBox, FSPalette.PanelDeep);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Top, tipBox.Right, tipBox.Top + 1f), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Bottom - 1f, tipBox.Right, tipBox.Bottom), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Left, tipBox.Top, tipBox.Left + 1f, tipBox.Bottom), sepColor);
            screen.DrawRect(new UIBox2(tipBox.Right - 1f, tipBox.Top, tipBox.Right, tipBox.Bottom), sepColor);

            var tx = tipX + tipPad;
            var ty = tipY + tipPad;
            screen.DrawString(_tooltipNameFont!, new Vector2(tx, ty), def.Name, Color.White);
            ty += nameDims.Y + 4f;
            screen.DrawString(_tooltipBodyFont!, new Vector2(tx, ty), levelText, FSPalette.Money);
            ty += levelDims.Y + 4f;
            screen.DrawString(_tooltipBodyFont!, new Vector2(tx, ty), effectText, muted);
            break;
        }

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
            screen.DrawRect(tipBox, FSPalette.PanelDeep);
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

        for (var i = _healPayouts.Count - 1; i >= 0; i--)
        {
            var h = _healPayouts[i];
            var updated = h with { Life = h.Life - args.DeltaSeconds };
            if (updated.Life <= 0f)
                _healPayouts.RemoveAt(i);
            else
                _healPayouts[i] = updated;
        }

        if (DirectiveFlash > 0f)
            DirectiveFlash = MathF.Max(0f, DirectiveFlash - args.DeltaSeconds * 2f);

        if (_respawnClickCooldown > 0f)
            _respawnClickCooldown -= args.DeltaSeconds;

        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        var mousePos = _input.MouseScreenPosition.Position;

        if (IsReadyUpVisible && down && !_prevClickDown)
        {
            if (ReadyUpYesBounds.Contains(mousePos))
                OnReadyUpClicked?.Invoke(true);
            else if (ReadyUpNoBounds.Contains(mousePos))
                OnReadyUpClicked?.Invoke(false);
        }

        if (IsRespawnOfferVisible && down && !_prevClickDown && _respawnClickCooldown <= 0f
            && RespawnButtonBounds.Contains(mousePos))
        {
            _respawnClickCooldown = 0.5f;
            OnRespawnClicked?.Invoke();
        }

        if (down && !_prevClickDown)
            HandleThrowableClick(mousePos);

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
        {
            DrawBuffStatus(screen, margin, null);
            return;
        }

        _tinyFont ??= new VectorFont(_notoRes ??= _resourceCache.GetResource<FontResource>(NotoBoldPath), 11);

        const float rowGap = 4f;
        const float iconTextGap = 4f;
        const float bottomGap = 10f;
        var textH = screen.GetDimensions(_tinyFont, "Ay", 1f).Y;
        var iconSz = textH * 2f;
        var blockH = rows.Count * iconSz + (rows.Count - 1) * rowGap;

        var vitalsTop = _clyde.ScreenSize.Y - BottomBandLift - VitalsBlockHeight();
        var ceiling = MathF.Min(FindAlertsTop() ?? vitalsTop, vitalsTop);
        var blockBottom = ceiling - bottomGap;
        var x = margin;
        var y = blockBottom - blockH;

        DrawBuffStatus(screen, margin, y);

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

    private void DrawBuffStatus(DrawingHandleScreen screen, float margin, float? bonusBlockTop)
    {
        _tinyFont ??= new VectorFont(_notoRes ??= _resourceCache.GetResource<FontResource>(NotoBoldPath), 11);

        var bottom = bonusBlockTop ?? (FindHotbarTop() ?? _clyde.ScreenSize.Y - margin) - 10f;

        if (MedicalBuffs.Count > 0)
        {
            const float rowGap = 4f;
            const float iconTextGap = 4f;
            var textH = screen.GetDimensions(_tinyFont, "Ay", 1f).Y;
            var iconSz = textH * 2f;

            bottom -= 4f + MedicalBuffs.Count * iconSz + (MedicalBuffs.Count - 1) * rowGap;

            var y = bottom;
            foreach (var buff in MedicalBuffs)
            {
                var icon = GetStatIcon(buff.IconKey) ?? GetStatIcon(MedicalBuffFallbackIcon);
                if (icon != null)
                    screen.DrawTextureRect(icon, new UIBox2(margin, y, margin + iconSz, y + iconSz), Color.White);

                var value = buff.SecondsRemaining >= 0 ? $"{buff.SecondsRemaining}s" : string.Empty;
                var valueDims = screen.GetDimensions(_tinyFont, value, 1f);
                var valuePos = new Vector2(margin + iconSz + iconTextGap, y + (iconSz - valueDims.Y) * 0.5f);
                screen.DrawString(_tinyFont, valuePos, value, MedicalBuffColour);

                var cellW = iconSz + iconTextGap + valueDims.X;
                _bonusRowCells.Add((new UIBox2(margin, y, margin + cellW, y + iconSz),
                    buff.Name, [buff.Source]));

                y += iconSz + rowGap;
            }
        }

        if (HarvestStatus is { } harvest)
        {
            const float gaugeW = 132f;
            const float barH = 4f;

            var dims = screen.GetDimensions(_tinyFont, harvest, 1f);
            bottom -= 4f + dims.Y + 3f + barH;

            var accent = HarvestCapped ? FSPalette.Money : FSPalette.Ok;
            screen.DrawString(_tinyFont, new Vector2(margin, bottom), harvest, accent);

            var ratio = HarvestCap > 0f ? Math.Clamp(HarvestAccrued / HarvestCap, 0f, 1f) : 0f;
            var barY = bottom + dims.Y + 3f;

            DrawRounded(screen, new UIBox2(margin, barY, margin + gaugeW, barY + barH),
                FSPalette.BarTrack, 2f);

            if (ratio > 0f)
            {
                DrawRounded(screen, new UIBox2(margin, barY, margin + gaugeW * ratio, barY + barH),
                    accent, 2f);
            }
        }
    }

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

    private static readonly Color VignetteWash = new(0.47f, 0f, 0f, 0.16f);
    private const float VignetteMaxDark = 0.72f;

    private static void DrawVignette(DrawingHandleScreen screen, Vector2i screenSize)
    {
        const int rings = 24;

        var w = (float) screenSize.X;
        var h = (float) screenSize.Y;

        screen.DrawRect(new UIBox2(0f, 0f, w, h), VignetteWash);

        for (var i = 0; i < rings; i++)
        {
            var t0 = i / (float) rings;
            var t1 = (i + 1) / (float) rings;

            var a = VignetteMaxDark * MathF.Pow(1f - t0, 2.2f);
            if (a <= 0.002f)
                continue;

            var dark = new Color(0f, 0f, 0f, a);

            var ox = w * 0.5f * t0;
            var oy = h * 0.5f * t0;
            var ix = w * 0.5f * t1;
            var iy = h * 0.5f * t1;

            screen.DrawRect(new UIBox2(ox, oy, w - ox, iy), dark);
            screen.DrawRect(new UIBox2(ox, h - iy, w - ox, h - oy), dark);
            screen.DrawRect(new UIBox2(ox, iy, ix, h - iy), dark);
            screen.DrawRect(new UIBox2(w - ix, iy, w - ox, h - iy), dark);
        }
    }

    private float? FindAlertsTop()
    {
        var screen = _uiManager.ActiveScreen;
        if (screen == null)
        {
            _alertsScreen = null;
            _alertsControl = null;
            return null;
        }

        if (!ReferenceEquals(screen, _alertsScreen) || _alertsControl is null || _alertsControl.Disposed)
        {
            _alertsScreen = screen;
            _alertsControl = FindNamedControlRecursive(screen, "Alerts", 0);
        }

        return _alertsControl == null ? null : _alertsControl.GlobalPixelRect.Top;
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

    private static readonly Color BonusPositive = FSPalette.Ok;
    private static readonly Color BonusNegative = FSPalette.Danger;

    private const string MedicalBuffFallbackIcon = "buff";
    private static readonly Color MedicalBuffColour = FSPalette.Ok;

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
