using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.RCD;
using Content.Shared._FinalStand.Repair;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Repair;

// Engineers see a segmented health bar under damaged fortifications: recent damage lingers, it flashes below 30%, and pips
// show the grade. Everyone sees upgrade progress and nearby crate ammo.
public sealed partial class FSStructureHealthOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly Color Back = new(0f, 0f, 0f, 0.75f);
    private static readonly Color Good = FSPalette.Ok;
    private static readonly Color Worn = FSPalette.Warn;
    private static readonly Color Critical = FSPalette.HealthLow;
    private static readonly Color CapMark = FSPalette.Danger;
    private static readonly Color Progress = Color.FromHex("#5fc8e8");
    private const float Width = 36f;
    private const float Height = 5f;
    private const int Segments = 6;
    private const float Gap = 1f;
    private const float CriticalFraction = 0.3f;
    private const float CrateRange = 2.5f;
    private const float CullMargin = 48f;
    private const float TrailHold = 0.45f;
    private const float TrailDrain = 0.9f;
    private static readonly Color CrateText = FSPalette.Money;

    private readonly Dictionary<EntityUid, (float Value, double HoldUntil, int Frame)> _trails = new();
    private readonly List<EntityUid> _stale = new();
    private int _frame;
    private double _lastTime;

    private Font? _font;

    private SharedTransformSystem? _xform;
    private SharedHandsSystem? _hands;
    private FSRepairFeedbackClientSystem? _feedback;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSStructureHealthOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity != null;
    }

    private bool IsEngineer()
    {
        return _player.LocalEntity is { } local
            && _entMan.TryGetComponent<FSDepartmentAccessComponent>(local, out var dept)
            && dept.Engineering;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var bounds = args.ViewportBounds;
        var s = _ui.DefaultUIScale;
        var compact = _cfg.GetCVar(CCVars.FSEngHealthBarCompact);
        var engineer = IsEngineer();
        var rivetCap = HeldRivetCap();
        var showAll = engineer && HoldingBuildTool();
        var now = _timing.RealTime.TotalSeconds;
        var dt = (float) Math.Clamp(now - _lastTime, 0d, 0.25d);
        _lastTime = now;
        _frame++;
        var flash = 0.55f + 0.45f * MathF.Sin((float) now * 8f);
        var width = Width * s;
        var height = MathF.Round((compact ? 3f : Height) * s);
        var segments = compact ? 1 : Segments;
        var gap = MathF.Max(1f, MathF.Round(Gap * s));

        var query = _entMan.EntityQueryEnumerator<FSStructureHealthComponent, DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var health, out var damageable, out var xform))
        {
            if (xform.MapID != args.MapId || health.Max <= 0f)
                continue;

            var center = Vector2.Transform(_xform.GetWorldPosition(xform), matrix);
            if (center.X < bounds.Left - CullMargin || center.X > bounds.Right + CullMargin
                || center.Y < bounds.Top - CullMargin || center.Y > bounds.Bottom + CullMargin)
                continue;

            var fraction = FSStructureHealth.Fraction(health, damageable);
            var trail = Trail(uid, fraction, now, dt);
            var (progress, pourColor) = UpgradeProgress(uid);
            if (!engineer)
            {
                if (progress > 0f)
                {
                    var half = (Vector2.Transform(_xform.GetWorldPosition(xform) + new Vector2(0f, 0.5f), matrix) - center).Length();
                    DrawProgress(handle, new Vector2(MathF.Round(center.X - width / 2f), MathF.Round(center.Y + half * 0.55f)), progress, pourColor, width, s);
                }
                continue;
            }

            if (fraction >= 1f && trail <= fraction && progress <= 0f && !showAll)
                continue;

            var color = fraction < CriticalFraction ? Critical.WithAlpha(flash) : fraction < 0.6f ? Worn : Good;
            if (fraction >= 1f)
                color = color.WithAlpha(0.55f);

            var full = FSWorldChip.Width(width, height, segments, gap);
            var tileHalf = (Vector2.Transform(_xform.GetWorldPosition(xform) + new Vector2(0f, 0.5f), matrix) - center).Length();
            var topLeft = new Vector2(MathF.Round(center.X - full / 2f), MathF.Round(center.Y + tileHalf * 0.55f));
            FSWorldChip.Bar(handle, topLeft, width, height, segments, gap, fraction, color, trail);

            if (!compact && Grade(uid) is { } grade)
                FSWorldChip.Pips(handle, topLeft + new Vector2(-3f * s, height / 2f), grade.Rank, MathF.Round(5f * s), grade.Color);

            if (progress > 0f)
                DrawProgress(handle, topLeft with { Y = topLeft.Y + height + 3f * s }, progress, pourColor, full, s);

            if (rivetCap is { } cap)
            {
                var slant = MathF.Round(height * 0.6f);
                var markX = MathF.Round(topLeft.X + slant / 2f + (full - slant) * cap);
                var pulse = CapPulse(uid);
                var reach = (2f + 4f * pulse) * s;
                var markColor = Color.InterpolateBetween(CapMark, Color.White, pulse);
                handle.DrawRect(new UIBox2(markX - 1f, topLeft.Y - reach - 1f, markX + 2f, topLeft.Y + height + reach + 1f), Back);
                handle.DrawRect(new UIBox2(markX, topLeft.Y - reach, markX + 1f + MathF.Round(pulse * 2f), topLeft.Y + height + reach), markColor);
            }
        }

        PruneTrails();
        if (!_cfg.GetCVar(CCVars.FSEngWorldChips))
            DrawCrates(handle, matrix, args.MapId, showAll);
    }

    private float CapPulse(EntityUid uid)
    {
        _feedback ??= _entMan.System<FSRepairFeedbackClientSystem>();
        if (_feedback.CapTarget != uid)
            return 0f;

        var since = (float) (_timing.RealTime - _feedback.CapAt).TotalSeconds;
        return since is >= 0f and < 0.5f ? 1f - since / 0.5f : 0f;
    }

    // Recent damage lingers as a white chunk, then drains down to the real value.
    private float Trail(EntityUid uid, float fraction, double now, float dt)
    {
        if (!_trails.TryGetValue(uid, out var trail) || fraction >= trail.Value)
        {
            _trails[uid] = (fraction, 0d, _frame);
            return fraction;
        }

        var value = trail.Value;
        var hold = trail.HoldUntil;
        if (hold <= 0d)
            hold = now + TrailHold;
        else if (now >= hold)
            value = MathF.Max(fraction, value - TrailDrain * dt);

        _trails[uid] = (value, value <= fraction ? 0d : hold, _frame);
        return value;
    }

    private void PruneTrails()
    {
        if (_frame % 120 != 0)
            return;

        foreach (var (uid, trail) in _trails)
        {
            if (_frame - trail.Frame > 60)
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
            _trails.Remove(uid);
        _stale.Clear();
    }

    private void DrawProgress(DrawingHandleScreen handle, Vector2 topLeft, float progress, Color color, float width, float s)
    {
        FSWorldChip.Rail(handle, topLeft, width, MathF.Max(2f, MathF.Round(3f * s)), progress, color);
    }

    private (int Rank, Color Color)? Grade(EntityUid uid)
    {
        if (!_entMan.TryGetComponent<FSUpgradeableComponent>(uid, out var upgradeable)
            || !_proto.TryIndex(upgradeable.Grade, out var grade)
            || grade.HudColor is not { } color)
            return null;

        var rank = 1;
        var at = grade;
        while (at.Requires is { } previous && _proto.TryIndex(previous, out at))
            rank++;

        return (rank, color);
    }

    private void DrawCrates(DrawingHandleScreen handle, Matrix3x2 matrix, MapId map, bool showAll)
    {
        if (_player.LocalEntity is not { } local)
            return;

        _font ??= new VectorFont(_cache.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf"), 9);
        var me = _xform!.GetWorldPosition(local);
        var crates = _entMan.EntityQueryEnumerator<FSAmmoCrateComponent, TransformComponent>();
        while (crates.MoveNext(out _, out var crate, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            if (!showAll && (pos - me).Length() > CrateRange)
                continue;

            var text = crate.Rounds.ToString("N0");
            var screen = Vector2.Transform(pos, matrix);
            var size = handle.GetDimensions(_font, text, 1f);
            var at = new Vector2(screen.X - size.X / 2f, screen.Y - 30f);
            handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(3f, 1f), size + new Vector2(6f, 2f)), Back);
            handle.DrawString(_font, at, text, crate.Rounds > 0 ? CrateText : Critical);
        }
    }

    // How far the pending upgrade is poured, in the colour of what it will become.
    private (float Progress, Color Color) UpgradeProgress(EntityUid uid)
    {
        if (!_entMan.TryGetComponent<FSUpgradeableComponent>(uid, out var upgradeable)
            || upgradeable.Pending is not { } pending
            || upgradeable.Poured <= 0
            || !_proto.TryIndex(pending, out var upgrade))
            return (0f, Progress);

        return ((float) upgradeable.Poured / upgrade.Hits, upgrade.HudColor ?? Progress);
    }

    // With a hammer or RCD in hand every fortification shows its bar, so the whole line reads at a glance.
    private bool HoldingBuildTool()
    {
        _hands ??= _entMan.System<SharedHandsSystem>();
        return _player.LocalEntity is { } local
               && _hands.TryGetActiveItem(local, out var held)
               && (_entMan.HasComponent<FSEngineerHammerComponent>(held) || _entMan.HasComponent<FSEngineerOnlyRCDComponent>(held));
    }

    private float? HeldRivetCap()
    {
        _hands ??= _entMan.System<SharedHandsSystem>();
        if (_player.LocalEntity is not { } local
            || !_hands.TryGetActiveItem(local, out var held)
            || !_entMan.TryGetComponent<FSRivetGunComponent>(held, out var gun))
            return null;

        return gun.RepairCap;
    }
}
