// The pilot HUD, drawn around the mech in the style of Mecha Break: segmented hull and power gauges with guide rails,
// slanted weapon cards with key badges, ammo and a cooldown sweep, and siege or low-power callouts underneath.
// It boots up when you climb in, flinches on hull hits, slides the card you swap to, and powers down when you leave.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Mech;
using Content.Shared.CCVar;
using Content.Shared.Mech.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechHudOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly Color Hull = Color.FromHex("#e8ecef");
    private static readonly Color Power = Color.FromHex("#52d6ef");
    private static readonly Color Dim = Color.White.WithAlpha(0.13f);
    private static readonly Color Rail = Color.White.WithAlpha(0.24f);
    private static readonly Color Tick = Color.White.WithAlpha(0.35f);
    private static readonly Color CardBack = Color.FromHex("#080a0c");
    private static readonly Color Sweep = Color.FromHex("#d2dae0").WithAlpha(0.16f);
    private static readonly Color Shadow = Color.Black.WithAlpha(0.35f);
    private static readonly ResPath LabelPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly ResPath NumberPath = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf");

    private const int Segments = 18;
    private const float Span = 62f;
    private const float Gap = 1.6f;
    private const float CardWidth = 140f;
    private const float CardHeight = 30f;
    private const float CardSlant = 9f;
    private const float CardStep = 36f;
    private const double BootTime = 1.6;
    private const double HitTime = 0.35;
    private const double SwapTime = 0.25;
    private const double DownTime = 1.8;
    private const float EaseRate = 7f;

    private readonly Dictionary<EntityUid, (TimeSpan Start, TimeSpan End)> _cooldowns = new();
    private FSMechHudSystem? _hud;
    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;
    private Font? _caption;
    private Font? _readout;
    private Font? _ammo;
    private Font? _key;
    private float _fontScale;

    private EntityUid? _active;
    private double _bootAt;
    private double _lastTime;
    private float _hullShown;
    private float _powerShown;
    private float _lastHull;
    private double _hitAt = double.MinValue;
    private EntityUid? _selected;
    private double _swapAt = double.MinValue;
    private double _downAt = double.MinValue;
    private bool _lost;
    private bool _broken;
    private EntityUid? _downMech;
    private Vector2 _downCenter;
    private float _downRadius;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    // Where the combat button goes, above the weapon cards, in screen pixels; null when not piloting.
    public UIBox2? CombatBox { get; private set; }

    public FSMechHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _hud ??= _entMan.System<FSMechHudSystem>();
        if (args.ViewportControl == null)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _sprite ??= _entMan.System<SpriteSystem>();
        var s = _ui.DefaultUIScale;
        LoadFonts(s);
        var now = _timing.RealTime.TotalSeconds;
        var dt = (float) Math.Clamp(now - _lastTime, 0d, 0.1d);
        _lastTime = now;
        var motion = _cfg.GetCVar(CCVars.FSEngMechHudMotion);
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        if (_hud.Piloted() is not { } mech)
        {
            CombatBox = null;
            if (_active != null)
            {
                _downAt = now;
                _downMech = _active;
                _lost = _broken;
                _active = null;
            }

            if (motion)
                PowerDown(handle, matrix, now, s);
            return;
        }

        var world = _xform.GetMapCoordinates(mech);
        if (world.MapId != args.MapId)
            return;

        var comp = mech.Comp;
        var hull = comp.MaxIntegrity > 0 ? Math.Clamp((comp.Integrity / comp.MaxIntegrity).Float(), 0f, 1f) : 0f;
        var power = comp.MaxEnergy > 0 ? Math.Clamp((comp.Energy / comp.MaxEnergy).Float(), 0f, 1f) : 0f;

        if (_active != mech.Owner)
        {
            _active = mech.Owner;
            _bootAt = now;
            _hullShown = motion ? 0f : hull;
            _powerShown = motion ? 0f : power;
            _lastHull = hull;
            _selected = comp.CurrentSelectedEquipment;
            _downAt = double.MinValue;
        }

        if (hull < _lastHull - 0.015f)
            _hitAt = now;
        _lastHull = hull;
        _broken = comp.Broken;

        if (comp.CurrentSelectedEquipment != _selected)
        {
            _selected = comp.CurrentSelectedEquipment;
            _swapAt = now;
        }

        var ease = motion ? 1f - MathF.Exp(-dt * EaseRate) : 1f;
        _hullShown += (hull - _hullShown) * ease;
        _powerShown += (power - _powerShown) * ease;

        var boot = motion ? (float) Math.Clamp((now - _bootAt) / 0.3, 0d, 1d) : 1f;
        var hit = motion ? (float) Math.Clamp(1d - (now - _hitAt) / HitTime, 0d, 1d) : 0f;

        var c = Vector2.Transform(world.Position, matrix);
        var tile = (Vector2.Transform(world.Position + Vector2.UnitX, matrix) - c).Length();
        var radius = tile * 1.65f;
        _downCenter = c;
        _downRadius = radius;

        var hullColor = hull < 0.25f ? FSPalette.Danger : hull < 0.5f ? FSPalette.Warn : Hull;
        var powerColor = power < 0.25f ? FSPalette.Danger : Power;
        var hullDrawn = Color.InterpolateBetween(hullColor, FSPalette.HealthLow, hit);
        var shake = new Vector2(MathF.Sin((float) now * 90f) * 3f * s * hit, 0f);

        Gauge(handle, c + shake, radius, 180f, 1f, _hullShown, hullDrawn, boot, s);
        Gauge(handle, c, radius, 0f, -1f, _powerShown, powerColor, boot, s);

        var readY = c.Y + radius * 0.42f;
        Readout(handle, new Vector2(c.X - radius - 18f * s, readY) + shake, Loc.GetString("fs-mech-hud-hull"), Percent(_hullShown),
            Faded(hullDrawn, boot), true, s);
        Readout(handle, new Vector2(c.X + radius + 18f * s, readY), Loc.GetString("fs-mech-hud-power"), Percent(_powerShown),
            Faded(powerColor, boot), false, s);

        var cardsAt = new Vector2(c.X + radius + 40f * s, c.Y - radius * 0.92f);
        Cards(handle, comp, cardsAt, now, motion, s);
        CombatBox = UIBox2.FromDimensions(cardsAt - new Vector2(0f, 28f * s), new Vector2(CardWidth * s, 22f * s));
        Callouts(handle, mech, new Vector2(c.X, c.Y + radius + 16f * s), power, now, motion, s);
    }

    // After you leave the seat the gauges drain and fade where the mech stood; a wreck also flashes MECH LOST.
    private void PowerDown(DrawingHandleScreen handle, Matrix3x2 matrix, double now, float s)
    {
        var t = (now - _downAt) / DownTime;
        if (t is < 0d or >= 1d)
            return;

        var c = _downCenter;
        if (_downMech is { } gone && _entMan.EntityExists(gone))
            c = Vector2.Transform(_xform!.GetWorldPosition(gone), matrix);

        var drain = (float) Math.Clamp(1d - (now - _downAt) / 0.45, 0d, 1d);
        var fade = (float) (1d - t);
        if (drain > 0f)
        {
            Gauge(handle, c, _downRadius, 180f, 1f, _hullShown * drain, Hull, drain, s);
            Gauge(handle, c, _downRadius, 0f, -1f, _powerShown * drain, Power, drain, s);
        }

        if (_lost && (int) (now * 4) % 2 == 0)
            Pill(handle, new Vector2(c.X, c.Y + _downRadius + 16f * s), Loc.GetString("fs-mech-hud-lost"), Faded(FSPalette.Danger, fade), s);
    }

    private void LoadFonts(float s)
    {
        if (_caption != null && Math.Abs(_fontScale - s) < 0.01f)
            return;

        _fontScale = s;
        var label = _cache.GetResource<FontResource>(LabelPath);
        var number = _cache.GetResource<FontResource>(NumberPath);
        _caption = new VectorFont(label, (int) MathF.Round(7 * s));
        _key = new VectorFont(label, (int) MathF.Round(7 * s));
        _readout = new VectorFont(number, (int) MathF.Round(15 * s));
        _ammo = new VectorFont(number, (int) MathF.Round(13 * s));
    }

    private static string Percent(float value) => ((int) MathF.Round(value * 100f)).ToString();

    private static void Gauge(DrawingHandleScreen handle, Vector2 c, float radius, float middle, float side, float fraction,
        Color color, float alpha, float s)
    {
        var width = 7f * s;
        var bottom = middle - side * Span / 2f;
        var step = Span / Segments;
        var lit = (int) MathF.Ceiling(fraction * Segments);
        FSShapes.Arc(handle, c, radius + 2f * s, width + 4f * s, middle - Span / 2f - 1f, middle + Span / 2f + 1f, Faded(Shadow, alpha));
        for (var i = 0; i < Segments; i++)
        {
            var a0 = bottom + side * (i * step + Gap / 2f);
            var a1 = bottom + side * ((i + 1) * step - Gap / 2f);
            var tip = i == lit - 1;
            FSShapes.Arc(handle, c, radius + (tip ? 2f * s : 0f), width + (tip ? 2f * s : 0f),
                MathF.Min(a0, a1), MathF.Max(a0, a1), Faded(i < lit ? color : Dim, alpha));
        }

        var rail = radius + 12f * s;
        FSShapes.Arc(handle, c, rail, 1.5f * s, middle - 42f, middle + 42f, Faded(Rail, alpha));
        FSShapes.Arc(handle, c, radius - width - 5f * s, 1f * s, middle - 34f, middle + 34f, Faded(Dim, alpha));
        for (var k = -4; k <= 4; k++)
        {
            var a = MathHelper.DegreesToRadians(middle + k * 10f);
            var dir = new Vector2(MathF.Cos((float) a), MathF.Sin((float) a));
            var length = (k is -4 or 0 or 4 ? 5f : 3f) * s;
            handle.DrawLine(c + dir * rail, c + dir * (rail + length), Faded(Tick, alpha));
        }
    }

    private static Color Faded(Color color, float alpha) => color.WithAlpha(color.A * alpha);

    private void Readout(DrawingHandleScreen handle, Vector2 at, string caption, string value, Color color, bool right, float s)
    {
        var captionSize = handle.GetDimensions(_caption!, caption, 1f);
        var valueSize = handle.GetDimensions(_readout!, value, 1f);
        handle.DrawString(_caption!, new Vector2(right ? at.X - captionSize.X : at.X, at.Y - captionSize.Y), caption, color.WithAlpha(0.6f));
        handle.DrawString(_readout!, new Vector2(right ? at.X - valueSize.X : at.X, at.Y - 2f * s), value, color);
    }

    private void Cards(DrawingHandleScreen handle, MechComponent mech, Vector2 origin, double real, bool motion, float s)
    {
        var now = _timing.CurTime;
        var width = CardWidth * s;
        var height = CardHeight * s;
        var slant = CardSlant * s;
        var equipment = mech.EquipmentContainer.ContainedEntities;
        for (var i = 0; i < equipment.Count && i < FSMechHudSystem.WeaponKeys; i++)
        {
            var item = equipment[i];
            var selected = mech.CurrentSelectedEquipment == item;
            var enter = motion ? EaseOut((float) Math.Clamp((real - _bootAt - 0.15 - i * 0.08) / 0.25, 0d, 1d)) : 1f;
            if (enter <= 0f)
                continue;

            var swap = selected && motion ? EaseOut((float) Math.Clamp((real - _swapAt) / SwapTime, 0d, 1d)) : 1f;
            var at = origin + new Vector2((selected ? -10f * s * swap : 0f) + (1f - enter) * 48f * s, i * CardStep * s);
            var accent = selected ? Power : Color.White.WithAlpha(0.18f);

            FSShapes.Slanted(handle, at + new Vector2(2f, 3f) * s, width, height, slant, Shadow);
            FSShapes.Slanted(handle, at, width, height, slant, CardBack.WithAlpha(selected ? 0.66f : 0.48f));
            if (selected && swap < 1f)
                FSShapes.Slanted(handle, at - new Vector2(2f, 2f) * s, width + 4f * s, height + 4f * s, slant,
                    Power.WithAlpha(0.5f * (1f - swap)), border: 2f * s);
            if (Cooldown(item, now) is { } progress)
                FSShapes.Slanted(handle, at, width, height, slant, Sweep, fill: progress);
            FSShapes.Slanted(handle, at, width, height, slant, accent, border: 1.5f * s);
            if (selected)
                FSShapes.Slanted(handle, at, slant + 4f * s, height, slant, Power);

            var keyCenter = at + new Vector2(24f * s, height / 2f);
            FSShapes.Diamond(handle, keyCenter, 16f * s, selected ? Power : Color.White.WithAlpha(0.45f), border: 1.5f * s);
            CenterDigit(handle, (i + 1).ToString(), keyCenter, selected ? Power : FSPalette.TextBright);

            if (_entMan.GetComponentOrNull<MetaDataComponent>(item)?.EntityPrototype is { } proto)
            {
                var icon = _sprite!.GetPrototypeIcon(proto.ID).Default;
                var fit = MathF.Min(54f * s / icon.Width, 18f * s / icon.Height);
                var size = new Vector2(icon.Width, icon.Height) * fit;
                handle.DrawTextureRect(icon, UIBox2.FromDimensions(at + new Vector2(38f * s, (height - size.Y) / 2f), size),
                    Color.White.WithAlpha(selected ? 1f : 0.6f));
            }

            var ammo = new GetAmmoCountEvent();
            _entMan.EventBus.RaiseLocalEvent(item, ref ammo);
            var text = ammo.Capacity > 0 ? ammo.Count.ToString() : "-";
            var textSize = handle.GetDimensions(_ammo!, text, 1f);
            var empty = ammo.Capacity > 0 && ammo.Count == 0;
            handle.DrawString(_ammo!, at + new Vector2(width - slant - 8f * s - textSize.X, (height - textSize.Y) / 2f), text,
                empty ? FSPalette.Danger : selected ? FSPalette.TextBright : FSPalette.TextMuted);
        }
    }

    private float? Cooldown(EntityUid gun, TimeSpan now)
    {
        if (!_entMan.TryGetComponent<GunComponent>(gun, out var comp))
            return null;

        if (!_cooldowns.TryGetValue(gun, out var window) || comp.NextFire > window.End + TimeSpan.FromMilliseconds(10))
        {
            window = (now, comp.NextFire);
            _cooldowns[gun] = window;
        }

        if (now >= window.End || window.End <= window.Start)
            return null;

        return (float) ((now - window.Start) / (window.End - window.Start));
    }

    private void CenterDigit(DrawingHandleScreen handle, string digit, Vector2 center, Color color)
    {
        var width = handle.GetDimensions(_key!, digit, 1f).X;
        var ascent = _key!.GetAscent(1f);
        handle.DrawString(_key!, new Vector2(center.X - width / 2f, center.Y - ascent * 0.64f), digit, color);
    }

    private void Callouts(DrawingHandleScreen handle, Entity<MechComponent> mech, Vector2 at, float power, double now, bool motion, float s)
    {
        var online = now - _bootAt;
        if (motion && online is > 0.35 and < BootTime)
        {
            var fade = (float) Math.Clamp((BootTime - online) / 0.4, 0d, 1d);
            Pill(handle, at, Loc.GetString("fs-mech-hud-online"), Power.WithAlpha(fade), s);
            at += new Vector2(0f, 22f * s);
        }

        if (mech.Comp.Broken && (int) (now * 4) % 2 == 0)
        {
            Pill(handle, at, Loc.GetString("fs-mech-hud-breached"), FSPalette.Danger, s);
            at += new Vector2(0f, 22f * s);
        }

        if (_entMan.TryGetComponent<FSSiegeModeComponent>(mech, out var siege) && siege.Planted)
        {
            Pill(handle, at, Loc.GetString("fs-mech-hud-siege"), Power, s);
            at += new Vector2(0f, 22f * s);
        }

        if (power < 0.3f && (int) (_timing.RealTime.TotalSeconds * 2) % 2 == 0)
            Pill(handle, at, Loc.GetString("fs-mech-hud-low-power"), FSPalette.Danger, s);
    }

    private void Pill(DrawingHandleScreen handle, Vector2 at, string text, Color color, float s)
    {
        var size = handle.GetDimensions(_caption!, text, 1f);
        var width = size.X + 26f * s;
        var height = 18f * s;
        var topLeft = at - new Vector2(width / 2f, 0f);
        FSShapes.Slanted(handle, topLeft + new Vector2(2f, 2f) * s, width, height, 6f * s, Faded(Shadow, color.A));
        FSShapes.Slanted(handle, topLeft, width, height, 6f * s, CardBack.WithAlpha(0.6f * color.A));
        FSShapes.Slanted(handle, topLeft, width, height, 6f * s, color, border: 1.5f * s);
        handle.DrawString(_caption!, new Vector2(at.X - size.X / 2f, topLeft.Y + (height - size.Y) / 2f), text, color);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
}
