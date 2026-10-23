using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.WaveHud;
using System.Numerics;
using Content.Client._FinalStand.Pings;
using Content.Client._FinalStand.Shop;
using Content.Client._FinalStand.UI;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared;
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
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;

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

    private float _panelW0 = 150f;

    private string _layoutRaw = "";
    private bool _isSeparatedLayout;

    // Window-width responsiveness times the player's UI Scale option; raw-pixel drawing reads this.
    private float _hudScale = 1f;
    private float _hudResolutionFactor = 1f;

    private readonly Dictionary<(string Text, Font Font, int Width), string> _fitCache = new();

    private string FitText(DrawingHandleScreen screen, Font font, string text, float maxWidth)
    {
        var key = (text, font, (int) maxWidth);
        if (_fitCache.TryGetValue(key, out var cached))
            return cached;

        if (_fitCache.Count > 256)
            _fitCache.Clear();

        return _fitCache[key] = TrimToWidth(screen, font, text, maxWidth);
    }

    private static string TrimToWidth(DrawingHandleScreen screen, Font font, string text, float maxWidth)
    {
        if (maxWidth <= 0f || screen.GetDimensions(font, text, 1f).X <= maxWidth)
            return text;

        for (var length = text.Length - 1; length > 0; length--)
        {
            var trimmed = text[..length].TrimEnd() + "…";
            if (screen.GetDimensions(font, trimmed, 1f).X <= maxWidth)
                return trimmed;
        }

        return string.Empty;
    }

    public static float GetResolutionFactor(float screenWidth)
    {
        const float refWidth = 1920f;
        return Math.Clamp(screenWidth / refWidth, 0.45f, 1.0f);
    }

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
    public FSBonusCategory CritChance;
    public FSBonusCategory CritDamage;
    public FSBonusCategory AttackSpeed;
    public FSBonusCategory Resistance;
    public FSBonusCategory MoveSpeed;

    private int _bonusVersion;

    public void SetBonusSummary(FSPlayerBonusSummaryEvent ev)
    {
        GunDamage = ev.GunDamage;
        FireRate = ev.FireRate;
        MeleeDamage = ev.MeleeDamage;
        ExplosiveDamage = ev.ExplosiveDamage;
        ReloadSpeed = ev.ReloadSpeed;
        MagazineSize = ev.MagazineSize;
        CritChance = ev.CritChance;
        CritDamage = ev.CritDamage;
        AttackSpeed = ev.AttackSpeed;
        Resistance = ev.Resistance;
        MoveSpeed = ev.MoveSpeed;
        _bonusVersion++;
    }

    public float PanelLeft = -1f;
    public float PanelTop = -1f;
    public float PanelWidth = 205f;
    public float RightColumnBottom = -1f;
    public float LeftColumnBottom = -1f;

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

    public readonly record struct MedicalBuffRow(string Name, string IconKey, int SecondsRemaining, string[] Tooltip);

    public readonly List<MedicalBuffRow> MedicalBuffs = new();

    public int MedicalFund;

    public string? ObjectiveLabel;
    public string ObjectiveValue = string.Empty;
    public Color ObjectiveColor = FSPalette.Warn;

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
    private bool _uiHadPointerLastFrame;

    private static readonly TextureLoadParameters LinearParams = new()
    {
        SampleParameters = new TextureSampleParameters { Filter = true },
    };

    private readonly Dictionary<string, Texture?> _augIconCache = new();
    private readonly List<(UIBox2 Cell, string Id)> _augCells = new();

    private readonly List<(UIBox2 Cell, string Label, string[] Tooltip)> _bonusRowCells = new();

    private readonly record struct EarningsRow(string PerkId, string? Credits, string? Research);
    private readonly record struct EarningsPopup(List<EarningsRow> Rows, float Life, float TotalLife);
    private readonly List<EarningsPopup> _earningsPopups = new();
    private float _creditsRowY;

    public void AddEarningsPopup(List<FSPerkEarningsEntry> entries)
    {
        const float life = 6f;
        var rows = new List<EarningsRow>(entries.Count);
        foreach (var entry in entries)
        {
            rows.Add(new EarningsRow(entry.PerkId,
                entry.WaveCredits > 0
                    ? Loc.GetString("fs-hud-earnings-credits", ("wave", entry.WaveCredits.ToString("N0")), ("game", entry.GameCredits.ToString("N0")))
                    : null,
                entry.WaveResearch > 0
                    ? Loc.GetString("fs-hud-earnings-research", ("wave", entry.WaveResearch.ToString("N0")), ("game", entry.GameResearch.ToString("N0")))
                    : null));
        }

        _earningsPopups.Clear();
        _earningsPopups.Add(new EarningsPopup(rows, life, life));
    }

    public void AddHealPayout(int amount, bool diminished)
    {
        const float life = 1.6f;
        var text = Loc.GetString("fs-hud-heal-payout", ("amount", amount.ToString("N0")));
        _healPayouts.Add(new HealPayout(text, diminished, life, life));
    }

    private readonly record struct HealPayout(string Text, bool Diminished, float Life, float TotalLife);
    private readonly List<HealPayout> _healPayouts = new();

    private readonly string _labelWave = Loc.GetString("fs-hud-wave");
    private readonly string _labelSurvive = Loc.GetString("fs-hud-survive");
    private readonly string _labelEnemiesLeft = Loc.GetString("fs-hud-enemies-left");
    private readonly string _labelNext = Loc.GetString("fs-hud-next");
    private readonly string _labelReadyUp = Loc.GetString("fs-hud-ready-up");
    private readonly string _labelYes = Loc.GetString("fs-hud-ready-yes");
    private readonly string _labelNo = Loc.GetString("fs-hud-ready-no");
    private readonly string _labelPerks = Loc.GetString("fs-hud-perks");
    private readonly string _labelDown = Loc.GetString("fs-hud-down");
    private readonly string _labelReviveNow = Loc.GetString("fs-hud-revive-now");
    private readonly string _labelNotUpgraded = Loc.GetString("fs-hud-perk-not-upgraded");

    private readonly FSHudText<int> _surviveText = new(s => $"{s / 60}:{s % 60:D2}");
    private readonly FSHudText<int> _prepText = new(s => $"{s / 60}:{s % 60:D2}");
    private readonly FSHudText<int> _fundText = new(amount => Loc.GetString("fs-hud-medical-fund", ("amount", amount)));
    private readonly FSHudText<int> _respawnText = new(cost => Loc.GetString("fs-hud-down-subline", ("cost", cost.ToString("N0"))));
    private readonly FSHudText<(int Ready, int Total)> _readyText =
        new(v => Loc.GetString("fs-hud-ready-count", ("ready", v.Ready), ("total", v.Total)));
    private readonly FSHudText<int> _perkLevelText =
        new(level => Loc.GetString("fs-hud-perk-level", ("level", level), ("max", FSPerkDef.MaxLevel)));
    private readonly Dictionary<int, string> _numberText = new();
    private readonly Dictionary<int, string> _secondsText = new();

    private string NumberText(int value)
    {
        if (!_numberText.TryGetValue(value, out var text))
            _numberText[value] = text = value.ToString();

        return text;
    }

    private string SecondsText(int seconds)
    {
        if (!_secondsText.TryGetValue(seconds, out var text))
            _secondsText[seconds] = text = Loc.GetString("fs-hud-seconds", ("seconds", seconds));

        return text;
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
        const float margin = ScreenMargin;

        var screen = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        var displayScale = _cfg.GetCVar(CVars.DisplayUIScale);
        var uiScale = displayScale > 0f ? displayScale : _uiManager.DefaultUIScale;

        _hudResolutionFactor = GetResolutionFactor(screenSize.X);
        var s = _hudResolutionFactor * uiScale;
        _hudScale = s;

        const float rowIconSz = 20f;
        const float augIconSz = 32f;

        var iconGap = MathF.Round(9f * s);
        var rowPad = MathF.Round(6f * s);
        const float sepH = 1f;
        var augGap = MathF.Round(3f * s);
        var panelW = MathF.Round(RightColumnWidth * s);

        var labelPt = Math.Max(9, (int)MathF.Round(11f * s));
        var valuePt = Math.Max(14, (int)MathF.Round(20f * s));

        EnsureHudIcons();

        var notoRes = _notoRes ??= _resourceCache.GetResource<FontResource>(NotoBoldPath);
        if (_cachedLabelPt != labelPt)
        {
            _labelFont = new VectorFont(notoRes, labelPt);
            _cachedLabelPt = labelPt;
            _cachedLabelH = screen.GetDimensions(_labelFont, _labelEnemiesLeft, 1f).Y;
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
        if (ObjectiveLabel != null) totalH += sepH + rowH;
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

        _panelW0 = panelW;

        var waveBox = new UIBox2(panelX, y, panelX + panelW, y + totalH);
        DrawPanel(screen, waveBox, VitalsBack, VitalsEdge, FSPalette.Danger);

        DrawBonusIndicator(screen, margin);
        DrawVitals(screen, margin, BottomBandLift);

        // Unlike FindHotbarTop(), which resolves a taller outer widget padded by unused vanilla containers.
        var storageTop = FindStoragePanelTop();
        var weaponLift = storageTop is { } storageTopY
            ? MathF.Max(0f, screenSize.Y - storageTopY + 6f * s)
            : (BottomBandLift + HotbarGui.StorageRowHeight + 6f) * uiScale;

        var weaponTop = DrawWeaponModuleAndGetTop(screen, rightEdge - margin, weaponLift);
        var throwH = DrawThrowables(screen, rightEdge - margin, weaponTop - 6f);
        RightStackHeight = screenSize.Y - BottomBandLift - (weaponTop - 6f - throwH);

        if (IsRespawnOfferVisible)
        {
            var headline = _labelDown;
            var subline = _respawnText.Get(RespawnCost);

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

            var btnLabel = _labelReviveNow;
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

            screen.DrawString(_labelFont!, new Vector2(panelX + panelInset, y), _labelReadyUp, muted);
            y += labelH + 2f;

            var countText  = ReadyUpTotal > 0 ? _readyText.Get((ReadyUpCount, ReadyUpTotal)) : "—";
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

            var yesDim = screen.GetDimensions(_labelFont!, _labelYes, 1f);
            var noDim  = screen.GetDimensions(_labelFont!, _labelNo,  1f);

            screen.DrawString(_labelFont!,
                new Vector2(btnX + (halfW - yesDim.X) * 0.5f, y + (btnH - yesDim.Y) * 0.5f),
                _labelYes, FSPalette.Ok);
            screen.DrawString(_labelFont!,
                new Vector2(btnX + halfW + 3f + (halfW - noDim.X) * 0.5f, y + (btnH - noDim.Y) * 0.5f),
                _labelNo, FSPalette.Danger);

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
            var textW = panelX + panelW - panelInset - textX;
            screen.DrawString(_labelFont!, new Vector2(textX, textStartY), FitText(screen, _labelFont!, label, textW), muted);
            screen.DrawString(font, new Vector2(textX, textStartY + labelH + 4f), FitText(screen, font, value, textW), valueColor);
            return y + sepH + rowH;
        }

        y = DrawRow(_iconWave, _labelWave, _waveText, FSPalette.Danger);

        if (IsDarkWave)
        {
            var secs = Math.Max(0f, DarkWaveSecondsRemaining);
            var survive = _surviveText.Get((int) secs);
            y = DrawRow(_iconEnemies, _labelSurvive, survive, FSPalette.Warn, _midFont);
        }
        else if (EnemiesTotal > 0)
        {
            y = DrawRow(_iconEnemies, _labelEnemiesLeft, _enemiesText, FSPalette.DangerSoft, _midFont);
        }

        if (ObjectiveLabel != null)
            y = DrawRow(_iconTimer, ObjectiveLabel, ObjectiveValue, ObjectiveColor, _labelFont);

        if (IsPrepPhase && PrepSecondsRemaining >= 0f)
        {
            var secs = (int)MathF.Ceiling(PrepSecondsRemaining);
            y = DrawRow(_iconTimer, _labelNext, _prepText.Get(secs), FSPalette.Warn, _midFont);
        }

        DrawReadyUpBlock();
        screen.DrawRect(new UIBox2(panelX + panelInset, y, panelX + panelW - panelInset, y + sepH), sepColor);

        var triageH = DrawTriage(screen, panelX, y + sepH + 10f);
        RightColumnBottom = triageH > 0f ? y + sepH + 10f + triageH : y + sepH;

        var topLeftTop = _isSeparatedLayout ? TopLeftYSeparated : TopLeftY;
        var leftX = TopLeftX + TopLeftPad;
        var leftY = topLeftTop;

        DrawShadowed(screen, _labelFont!, new Vector2(leftX, leftY), _labelPerks, muted);
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
                    var stackStr = NumberText(stacks);
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

        LeftColumnBottom = _creditsRowY + screen.GetDimensions(_valueFont!, _creditsText, 1f).Y;
        if (ShowMedicalFund)
        {
            var fundText = _fundText.Get(MedicalFund);
            DrawShadowed(screen, _labelFont!,
                new Vector2(leftX, LeftColumnBottom + 2f),
                fundText, FSPalette.Ok);
            LeftColumnBottom += 2f + screen.GetDimensions(_labelFont!, fundText, 1f).Y;
        }

        var creditsW = screen.GetDimensions(_valueFont!, _creditsText, 1f).X;
        for (var pi = 0; pi < _earningsPopups.Count; pi++)
        {
            var p = _earningsPopups[pi];
            var t = p.Life / p.TotalLife;
            var alpha = MathF.Min(1f, t * 6f);
            var floatOffset = (1f - t) * 18f;

            var popupIconSz = augIconSz * 0.75f;
            var popupX = leftX + creditsW + 10f;
            var rowY = _creditsRowY - floatOffset;

            foreach (var entry in p.Rows)
            {
                var tex = GetPerkIcon(entry.PerkId);
                if (tex != null)
                    screen.DrawTextureRect(tex,
                        new UIBox2(popupX, rowY, popupX + popupIconSz, rowY + popupIconSz),
                        Color.White.WithAlpha(alpha));

                var textX = popupX + popupIconSz + 4f;
                if (entry.Credits is { } text)
                {
                    var dim = screen.GetDimensions(_labelFont!, text, 1f);
                    screen.DrawString(_labelFont!, new Vector2(textX, rowY + (popupIconSz - dim.Y) * 0.5f),
                        text, FSPalette.Money.WithAlpha(alpha));
                    textX += dim.X + 8f;
                }
                if (entry.Research is { } research)
                {
                    var dim = screen.GetDimensions(_labelFont!, research, 1f);
                    screen.DrawString(_labelFont!, new Vector2(textX, rowY + (popupIconSz - dim.Y) * 0.5f),
                        research, FSPalette.StaminaFill.WithAlpha(alpha));
                }

                rowY += popupIconSz + 2f;
            }
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
            var text = h.Text;
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
            var levelText = _perkLevelText.Get(level);
            var effectText = level > 0 ? def.LevelEffect(level - 1) : _labelNotUpgraded;
            var name = def.Name;

            const float tipPad = 8f;
            const float tipW = 200f;

            var nameDims = screen.GetDimensions(_tooltipNameFont!, name, 1f);
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
            screen.DrawString(_tooltipNameFont!, new Vector2(tx, ty), name, Color.White);
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
        for (var i = _earningsPopups.Count - 1; i >= 0; i--)
        {
            var p = _earningsPopups[i];
            var updated = p with { Life = p.Life - args.DeltaSeconds };
            if (updated.Life <= 0f)
                _earningsPopups.RemoveAt(i);
            else
                _earningsPopups[i] = updated;
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

        // A ring, the ping wheel, a popup or a window's close button can shut on the click that picks from it, in
        // input handling, which runs before this. The UI having had the pointer the frame before keeps that click its own.
        var down = _input.IsKeyDown(Keyboard.Key.MouseLeft);
        var mousePos = _input.MouseScreenPosition.Position;
        var uiHasPointer = UiHasPointer();
        var pressed = down && !_prevClickDown && !uiHasPointer && !_uiHadPointerLastFrame;
        _uiHadPointerLastFrame = uiHasPointer;

        if (IsReadyUpVisible && pressed)
        {
            if (ReadyUpYesBounds.Contains(mousePos))
                OnReadyUpClicked?.Invoke(true);
            else if (ReadyUpNoBounds.Contains(mousePos))
                OnReadyUpClicked?.Invoke(false);
        }

        if (IsRespawnOfferVisible && pressed && _respawnClickCooldown <= 0f
            && RespawnButtonBounds.Contains(mousePos))
        {
            _respawnClickCooldown = 0.5f;
            OnRespawnClicked?.Invoke();
        }

        if (pressed)
            HandleThrowableClick(mousePos);

        _prevClickDown = down;
    }

    // The HUD is drawn under the UI, so a click on a window over it (the escape menu, a console, a shop) is that
    // window's. Otherwise opening the escape menu over the respawn button and clicking it buys the respawn. The build
    // and upgrade rings and the ping wheel take every click while they're open, and so does an open popup, which a
    // click anywhere else closes.
    private bool UiHasPointer()
    {
        foreach (var child in _uiManager.PopupRoot.Children)
        {
            if (child is FSRingControl or FSPingWheel)
                return true;
        }

        foreach (var child in _uiManager.ModalRoot.Children)
        {
            if (child.Visible)
                return true;
        }

        for (var control = _uiManager.CurrentlyHovered; control != null; control = control.Parent)
        {
            if (control is BaseWindow or Popup)
                return true;
        }

        return false;
    }

    private Font? _tinyFont;
    private readonly Dictionary<string, Texture?> _statIconCache = new();

    public const string PerkIconPrefix = "perk:";

    private Texture? GetStatIcon(string key)
    {
        if (_statIconCache.TryGetValue(key, out var cached))
            return cached;
        var path = key.StartsWith(PerkIconPrefix)
            ? $"/Textures/_FinalStand/Interface/Perks/Icons/{key[PerkIconPrefix.Length..]}.png"
            : $"/Textures/_FinalStand/Interface/HUD/hud_stat_{key}.png";
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

                var value = buff.SecondsRemaining >= 0 ? SecondsText(buff.SecondsRemaining) : string.Empty;
                var valueDims = screen.GetDimensions(_tinyFont, value, 1f);
                var valuePos = new Vector2(margin + iconSz + iconTextGap, y + (iconSz - valueDims.Y) * 0.5f);
                screen.DrawString(_tinyFont, valuePos, value, MedicalBuffColour);

                var cellW = iconSz + iconTextGap + valueDims.X;
                _bonusRowCells.Add((new UIBox2(margin, y, margin + cellW, y + iconSz),
                    buff.Name, buff.Tooltip));

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
        // StoragePanel sits 6 levels under the screen.
        if (depth > 8)
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

    private Control? _storagePanelControl;
    private Control? _storagePanelScreen;

    private float? FindStoragePanelTop()
    {
        var screen = _uiManager.ActiveScreen;
        if (screen == null)
        {
            _storagePanelScreen = null;
            _storagePanelControl = null;
            return null;
        }

        if (!ReferenceEquals(screen, _storagePanelScreen) || _storagePanelControl is null || _storagePanelControl.Disposed)
        {
            _storagePanelScreen = screen;
            _storagePanelControl = FindNamedControlRecursive(screen, "StoragePanel", 0);
        }

        if (_storagePanelControl is not { VisibleInTree: true } panel || panel.GlobalPixelRect.Height <= 0)
            return null;

        return panel.GlobalPixelRect.Top;
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

        if (holdingNonLauncherGun) AddPctRow(_bonusRows, "fs-hud-bonus-row-gun", "damage", GunDamage);
        if (holdingGun) AddPctRow(_bonusRows, "fs-hud-bonus-row-fire-rate", "firerate", FireRate);
        if (holdingMelee) AddPctRow(_bonusRows, "fs-hud-bonus-row-melee", "melee", MeleeDamage);
        if (holdingExplosive) AddPctRow(_bonusRows, "fs-hud-bonus-row-explosive", "explosive", ExplosiveDamage);
        if (holdingGun) AddPctRow(_bonusRows, "fs-hud-bonus-row-reload", "reload", ReloadSpeed);
        if (holdingGun) AddPctRow(_bonusRows, "fs-hud-bonus-row-mag-size", "magsize", MagazineSize);
        if (holdingGun || holdingMelee) AddPctRow(_bonusRows, "fs-hud-bonus-row-crit-chance", "crit", CritChance);
        if (holdingGun || holdingMelee) AddPctRow(_bonusRows, "fs-hud-bonus-row-crit-damage", "critdmg", CritDamage);
        if (holdingMelee) AddPctRow(_bonusRows, "fs-hud-bonus-row-attack-speed", "atkspeed", AttackSpeed);
        AddPctRow(_bonusRows, "fs-hud-bonus-row-resist", "resist", Resistance);
        AddPctRow(_bonusRows, "fs-hud-bonus-row-speed", "speed", MoveSpeed);

        return _bonusRows;
    }

    private static void AddPctRow(List<BonusRow> rows, string labelKey, string iconKey, FSBonusCategory cat)
    {
        if (MathF.Abs(cat.Percent) < 0.05f)
            return;
        var text = $"{(cat.Percent >= 0 ? "+" : "")}{cat.Percent:0.#}%";
        rows.Add(new BonusRow(Loc.GetString(labelKey), text, cat.Percent >= 0 ? BonusPositive : BonusNegative, cat.Sources, iconKey));
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
