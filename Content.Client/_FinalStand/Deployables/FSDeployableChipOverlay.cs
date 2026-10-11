// One chip style over deployables: sentry ammo, ammo-box uses, HMG crate rounds and time left. Full chips up close or
// with a build tool in hand, a bare bar from further away.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Deployables;

public sealed partial class FSDeployableChipOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private const string LockPath = "/Textures/Interface/VerbIcons/lock.svg.192dpi.png";
    private const float NearRange = 3f;
    private const float CrateRange = 2.5f;
    private const float CullMargin = 64f;
    private const float LowFraction = 0.3f;
    private const float TimeWarn = 10f;

    public static readonly Color Ammo = FSPalette.Money;
    public static readonly Color Low = FSPalette.Warn;
    public static readonly Color Empty = FSPalette.DangerSoft;
    private static readonly Color Supply = Color.FromHex("#7fb0d6");
    private static readonly Color ClockColor = FSPalette.TextBright;

    private readonly Dictionary<int, Font> _fonts = new();
    private SharedTransformSystem? _xform;
    private SharedMapSystem? _map;
    private SharedHandsSystem? _hands;
    private FSDeployableAlertClientSystem _alerts = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSDeployableChipOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null || _player.LocalEntity is not { } local || !_cfg.GetCVar(CCVars.FSEngWorldChips))
            return;

        _xform ??= _entMan.System<SharedTransformSystem>();
        _map ??= _entMan.System<SharedMapSystem>();
        _alerts ??= _entMan.System<FSDeployableAlertClientSystem>();
        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var s = _ui.DefaultUIScale;
        var me = _xform.GetWorldPosition(local);
        var tool = HoldingBuildTool(local);
        var font = Font(s);
        var ctx = new Ctx(handle, matrix, args.ViewportBounds, s, me, tool, font);

        var lifetimes = _entMan.GetEntityQuery<FSDeployableLifetimeComponent>();
        var sentries = _entMan.EntityQueryEnumerator<FSSentryTurretComponent, TransformComponent>();
        while (sentries.MoveNext(out var uid, out var sentry, out var xform))
        {
            if (xform.MapID != args.MapId || !xform.Anchored)
                continue;

            var fraction = sentry.MaxAmmo > 0 ? (float) sentry.Ammo / sentry.MaxAmmo : 0f;
            var color = sentry.Ammo <= 0 ? Empty : fraction < LowFraction ? Low : Ammo;
            Chip(ctx, uid, xform, fraction, color, sentry.Ammo.ToString(), 8, lifetimes, true, alert: _alerts.Pulse(uid));
        }

        var boxes = _entMan.EntityQueryEnumerator<FSAmmoBoxComponent, TransformComponent>();
        while (boxes.MoveNext(out var uid, out var box, out var xform))
        {
            if (xform.MapID != args.MapId || !xform.Anchored)
                continue;

            var fraction = box.MaxUses > 0 ? (float) box.UsesLeft / box.MaxUses : 0f;
            var at = Chip(ctx, uid, xform, fraction, box.UsesLeft > 0 ? Supply : Empty, $"{box.UsesLeft}/{box.MaxUses}",
                Math.Clamp(box.MaxUses, 1, 8), lifetimes, true);
            if (at is { } right && box.Private && FSShapes.Shrunk(LockPath, (int) MathF.Round(10f * s)) is { } lockIcon)
                handle.DrawTextureRect(lockIcon, UIBox2.FromDimensions(right + new Vector2(3f * s, -5f * s), new Vector2(lockIcon.Width, lockIcon.Height)), FSPalette.TextMuted);
        }

        var crates = _entMan.EntityQueryEnumerator<FSAmmoCrateComponent, TransformComponent>();
        while (crates.MoveNext(out var uid, out var crate, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!tool && (_xform.GetWorldPosition(xform) - me).Length() > CrateRange)
                continue;

            var fraction = crate.MaxRounds > 0 ? (float) crate.Rounds / crate.MaxRounds : 0f;
            Chip(ctx, uid, xform, fraction, crate.Rounds <= 0 ? Empty : fraction < LowFraction ? Low : Ammo,
                crate.Rounds.ToString("N0"), 8, lifetimes, true, timed: false);
        }

        var timers = _entMan.EntityQueryEnumerator<FSDeployableLifetimeComponent, TransformComponent>();
        while (timers.MoveNext(out var uid, out var lifetime, out var xform))
        {
            if (xform.MapID != args.MapId
                || _entMan.HasComponent<FSSentryTurretComponent>(uid)
                || _entMan.HasComponent<FSAmmoBoxComponent>(uid))
                continue;

            var left = Remaining(lifetime);
            var fraction = lifetime.Lifetime > TimeSpan.Zero ? (float) (left / lifetime.Lifetime) : 0f;
            Chip(ctx, uid, xform, fraction, left.TotalSeconds < TimeWarn ? Low : ClockColor, Clock(left), 1, lifetimes, true, timed: false);
        }
    }

    private readonly record struct Ctx(DrawingHandleScreen Handle, System.Numerics.Matrix3x2 Matrix, UIBox2i Bounds, float Scale,
        Vector2 Me, bool Tool, Font Font);

    // Draws one chip above the entity and returns the right edge of its plate, or null if it was culled.
    private Vector2? Chip(in Ctx ctx, EntityUid uid, TransformComponent xform, float fraction, Color color, string text,
        int segments, EntityQuery<FSDeployableLifetimeComponent> lifetimes, bool showText, bool timed = true, float alert = 0f)
    {
        var pos = _xform!.GetWorldPosition(xform);
        var screen = Vector2.Transform(pos, ctx.Matrix);
        if (screen.X < ctx.Bounds.Left - CullMargin || screen.X > ctx.Bounds.Right + CullMargin
            || screen.Y < ctx.Bounds.Top - CullMargin || screen.Y > ctx.Bounds.Bottom + CullMargin)
            return null;

        var s = ctx.Scale;
        var full = ctx.Tool || alert > 0f || (pos - ctx.Me).Length() <= NearRange;
        var tileHalf = (Vector2.Transform(pos + new Vector2(0f, 0.5f), ctx.Matrix) - screen).Length();
        var height = MathF.Round(5f * s);
        var gap = MathF.Max(1f, MathF.Round(s));
        var barWidth = MathF.Round((full ? 40f : 28f) * s);
        var bar = FSWorldChip.Width(barWidth, height, segments, gap);
        var textSize = full && showText ? ctx.Handle.GetDimensions(ctx.Font, text, 1f) : Vector2.Zero;
        var width = bar + (textSize.X > 0f ? textSize.X + 5f * s : 0f);
        var top = MathF.Round(screen.Y - tileHalf * 1.3f - height);
        if (FSWorldHudSpace.BarNorth(_entMan, _map!, xform))
            top = MathF.Min(top, MathF.Round(screen.Y - tileHalf * 1.45f - 4f * s - height / 2f - MathF.Max(textSize.Y, height) / 2f - s));
        var left = MathF.Round(screen.X - width / 2f);

        if (textSize.X > 0f)
        {
            var plate = new Vector2(left - 4f * s, MathF.Round(top + height / 2f - textSize.Y / 2f - 1f * s));
            FSShapes.Slanted(ctx.Handle, plate, width + 8f * s, textSize.Y + 2f * s, MathF.Round(4f * s), FSWorldChip.Back);
            ctx.Handle.DrawString(ctx.Font, new Vector2(left + bar + 5f * s, plate.Y + 1f * s), text, color);
        }

        if (alert > 0f)
            FSShapes.Slanted(ctx.Handle, new Vector2(left - 6f * s, top - 4f * s), width + 12f * s, height + 8f * s,
                MathF.Round(4f * s), Low.WithAlpha(0.35f + 0.5f * alert), border: MathF.Max(1f, 1.5f * s));

        FSWorldChip.Bar(ctx.Handle, new Vector2(left, top), barWidth, height, segments, gap, fraction, color);

        if (timed && lifetimes.TryComp(uid, out var lifetime) && lifetime.Lifetime > TimeSpan.Zero)
        {
            var left2 = Remaining(lifetime);
            var rail = (float) (left2 / lifetime.Lifetime);
            FSWorldChip.Rail(ctx.Handle, new Vector2(left, top + height + 3f * s), bar, MathF.Max(2f, MathF.Round(2f * s)),
                rail, left2.TotalSeconds < TimeWarn ? Low : ClockColor.WithAlpha(0.8f));
        }

        return new Vector2(left + width, top + height / 2f);
    }

    private TimeSpan Remaining(FSDeployableLifetimeComponent lifetime)
    {
        var left = lifetime.ExpiresAt - _timing.CurTime;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    private static string Clock(TimeSpan left) => $"{(int) left.TotalMinutes}:{left.Seconds:D2}";

    private Font Font(float scale)
    {
        var size = Math.Max(7, (int) MathF.Round(8f * scale));
        if (!_fonts.TryGetValue(size, out var font))
            _fonts[size] = font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf"), size);
        return font;
    }

    private bool HoldingBuildTool(EntityUid local)
    {
        _hands ??= _entMan.System<SharedHandsSystem>();
        return _entMan.TryGetComponent<FSDepartmentAccessComponent>(local, out var dept)
               && dept.Engineering
               && _hands.TryGetActiveItem(local, out var held)
               && (_entMan.HasComponent<FSEngineerHammerComponent>(held) || _entMan.HasComponent<FSEngineerOnlyRCDComponent>(held));
    }
}
