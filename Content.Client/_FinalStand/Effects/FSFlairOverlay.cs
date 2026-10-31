// World-space flair that is also information: build holograms filling in, the planted Broadsword's frontal barrier,
// the emplacement's cover zone, a faint line from each sentry to what it's shooting at, and a mech's low-power blink.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Effects;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.RCD;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Effects;

public sealed partial class FSFlairOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly Color HoloColor = Color.FromHex("#5fc8e8");
    private static readonly Color ShieldColor = Color.FromHex("#7fd8ff");
    private static readonly Color CoverColor = Color.FromHex("#d9c39a");
    private static readonly Color LaserColor = Color.FromHex("#ff5a4a");
    private static readonly Color AlarmColor = Color.FromHex("#ff2a2a");
    private static readonly SpriteSpecifier LowPowerIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Alerts/battery.rsi"), "battery0");
    private const float LowPowerIconSize = 0.5f;
    private const float LowPowerIconHeight = 1.45f;
    private const float ShieldRadius = 1.55f;
    private const int ShieldSegments = 28;
    private const float ShieldOpenTime = 0.35f;
    private const float ShieldCloseTime = 0.25f;
    private const double ShieldSweepTime = 2.4;
    private const float CoverRange = 8f;
    private const float RecoveryRange = 8f;

    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;
    private SharedAppearanceSystem? _appearance;
    private FSFlairClientSystem? _flair;
    private readonly Dictionary<EntityUid, ShieldState> _shields = new();
    private double _shieldTime;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FSFlairOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _xform ??= _entMan.System<SharedTransformSystem>();
        _sprite ??= _entMan.System<SpriteSystem>();
        _appearance ??= _entMan.System<SharedAppearanceSystem>();
        _flair ??= _entMan.System<FSFlairClientSystem>();

        var handle = args.WorldHandle;
        var now = _timing.RealTime;
        var pulse = 0.5f + 0.5f * MathF.Sin((float) now.TotalSeconds * 3f);

        DrawHolograms(handle, args, now);
        DrawShields(handle, args, pulse);
        DrawCover(handle, args);
        DrawRecoveryZones(handle, args);
        DrawSentryLines(handle, args);
        DrawLowPower(handle, args, now);
    }

    private void DrawHolograms(DrawingHandleWorld handle, in OverlayDrawArgs args, TimeSpan now)
    {
        foreach (var holo in _flair!.Holograms)
        {
            if (!_proto.TryIndex<RCDPrototype>(holo.Piece, out var piece) || piece.Prototype is not { } built)
                continue;

            var map = _xform!.ToMapCoordinates(holo.Coords);
            if (map.MapId != args.MapId)
                continue;

            var total = (float) (holo.End - holo.Start).TotalSeconds;
            var progress = total > 0f ? Math.Clamp((float) (now - holo.Start).TotalSeconds / total, 0f, 1f) : 1f;
            var texture = _sprite!.GetPrototypeIcon(built).Default;
            var size = new Vector2(texture.Width, texture.Height) / EyeManager.PixelsPerMeter;
            var grid = _xform.GetWorldRotation(holo.Coords.EntityId);
            var box = Box2.CenteredAround(map.Position, size);
            handle.DrawTextureRect(texture, new Box2Rotated(box, grid + holo.Rotation, map.Position), HoloColor.WithAlpha(0.25f + 0.45f * progress));

            var y = -size.Y / 2f + size.Y * progress;
            handle.DrawLine(map.Position + grid.RotateVec(new Vector2(-size.X / 2f, y)),
                map.Position + grid.RotateVec(new Vector2(size.X / 2f, y)), HoloColor.WithAlpha(0.9f));
        }
    }

    // The siege barrier: it unfolds from the centre when the mech plants and folds away when it lifts. A thick curved band
    // with a bright leading edge, a hex lattice that shimmers as a wave runs through it, a highlight sweeping across, emitter
    // nodes at both ends, and a flare whenever the mech takes a hit.
    private void DrawShields(DrawingHandleWorld handle, in OverlayDrawArgs args, float pulse)
    {
        var now = _timing.RealTime.TotalSeconds;
        var dt = (float) Math.Clamp(now - _shieldTime, 0d, 0.1d);
        _shieldTime = now;

        var query = _entMan.EntityQueryEnumerator<FSSiegeModeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var siege, out var xform))
        {
            if (!_shields.TryGetValue(uid, out var state))
            {
                if (!siege.Planted)
                    continue;

                _shields[uid] = state = new ShieldState();
            }

            state.Open = siege.Planted
                ? MathF.Min(1f, state.Open + dt / ShieldOpenTime)
                : MathF.Max(0f, state.Open - dt / ShieldCloseTime);

            var hull = _entMan.TryGetComponent<MechComponent>(uid, out var mech) ? mech.Integrity.Float() : 0f;
            if (siege.Planted && hull < state.Hull - 0.5f)
                state.FlashAt = now;
            state.Hull = hull;

            if (state.Open <= 0f)
            {
                _shields.Remove(uid);
                continue;
            }

            if (xform.MapID != args.MapId)
                continue;

            var open = 1f - (1f - state.Open) * (1f - state.Open) * (1f - state.Open);
            var flash = (float) Math.Clamp(1d - (now - state.FlashAt) / 0.35, 0d, 1d);
            var center = _xform!.GetWorldPosition(xform);
            var facing = _xform.GetWorldRotation(xform);
            var half = (float) Angle.FromDegrees(siege.FrontalArc / 2f).Theta * open;
            var outer = ShieldRadius * (0.8f + 0.2f * open);
            var inner = outer - 0.22f;
            var edge = ShieldColor.WithAlpha(1f);

            Band(handle, center, facing, half, 0f, inner, -1f, 1f, ShieldColor.WithAlpha(0.035f * open));
            Band(handle, center, facing, half, inner - 0.25f, inner, -1f, 1f, ShieldColor.WithAlpha(0.06f * open));
            Band(handle, center, facing, half, inner, outer, -1f, 1f, ShieldColor.WithAlpha((0.13f + 0.05f * pulse + 0.3f * flash) * open));
            Band(handle, center, facing, half, outer - 0.05f, outer + 0.015f, -1f, 1f, edge.WithAlpha((0.7f + 0.3f * flash) * open));
            Band(handle, center, facing, half, inner - 0.01f, inner + 0.015f, -1f, 1f, edge.WithAlpha(0.3f * open));

            var sweep = (float) (now / ShieldSweepTime % 1d) * 2.4f - 1.2f;
            Band(handle, center, facing, half, inner, outer, sweep - 0.12f, sweep + 0.12f, Color.White.WithAlpha(0.22f * open));

            Lattice(handle, center, facing, half, inner, outer, (float) now, open);

            foreach (var side in new[] { -1f, 1f })
            {
                var node = center + Dir(facing, half * side) * (outer - 0.11f);
                handle.DrawCircle(node, 0.07f, edge.WithAlpha(0.35f * open));
                handle.DrawCircle(node, 0.035f, Color.White.WithAlpha((0.8f + 0.2f * pulse) * open));
            }
        }
    }

    // A slice of ring between two radii, from one fraction of the arc to another (-1 is one end, 1 the other).
    private static void Band(DrawingHandleWorld handle, Vector2 center, Angle facing, float half, float r0, float r1,
        float from, float to, Color color)
    {
        from = Math.Clamp(from, -1f, 1f);
        to = Math.Clamp(to, -1f, 1f);
        if (to <= from || half <= 0f || color.A <= 0f)
            return;

        var steps = Math.Max(2, (int) MathF.Ceiling(ShieldSegments * (to - from) / 2f));
        var strip = new Vector2[(steps + 1) * 2];
        for (var i = 0; i <= steps; i++)
        {
            var dir = Dir(facing, half * (from + (to - from) * i / steps));
            strip[i * 2] = center + dir * r0;
            strip[i * 2 + 1] = center + dir * r1;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, strip, color);
    }

    // Two staggered rows of hex cells across the band; a wave travels along them so the barrier looks alive.
    private static void Lattice(DrawingHandleWorld handle, Vector2 center, Angle facing, float half, float inner, float outer,
        float time, float open)
    {
        var mid = (inner + outer) / 2f;
        var cell = 0.075f;
        var count = Math.Max(1, (int) (half * 2f * mid / (cell * 1.75f)));
        for (var row = 0; row < 2; row++)
        {
            var radius = mid + (row == 0 ? -cell * 0.45f : cell * 0.45f);
            for (var i = 0; i < count; i++)
            {
                var frac = (i + (row == 0 ? 0.25f : 0.75f)) / count * 2f - 1f;
                var wave = MathF.Max(0f, MathF.Sin(frac * 4f - time * 2.6f + row));
                var alpha = (0.07f + 0.4f * wave * wave) * open;
                var at = center + Dir(facing, half * frac) * radius;
                var spin = facing.Theta + half * frac;
                Vector2? last = null;
                for (var k = 0; k <= 6; k++)
                {
                    var a = spin + k * MathF.PI / 3f;
                    var p = at + new Vector2(MathF.Cos((float) a), MathF.Sin((float) a)) * cell * 0.5f;
                    if (last is { } prev)
                        handle.DrawLine(prev, p, ShieldColor.WithAlpha(alpha));
                    last = p;
                }
            }
        }
    }

    private static Vector2 Dir(Angle facing, float offset) => (facing + offset).ToWorldVec();

    private sealed class ShieldState
    {
        public float Open;
        public float Hull;
        public double FlashAt = double.MinValue;
    }

    private void DrawRecoveryZones(DrawingHandleWorld handle, in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;

        var me = _xform!.GetMapCoordinates(local);
        var query = _entMan.EntityQueryEnumerator<FSRecoveryZoneComponent, TransformComponent>();
        while (query.MoveNext(out _, out var zone, out var xform))
        {
            if (xform.MapID != args.MapId || xform.MapID != me.MapId)
                continue;

            var grid = xform.GridUid is { } gridUid ? _xform.GetWorldRotation(gridUid) : Angle.Zero;
            var center = _xform.GetWorldPosition(xform) + grid.RotateVec(zone.Offset);
            var distance = (center - me.Position).Length();
            if (distance > zone.Radius + RecoveryRange)
                continue;

            var alpha = distance <= zone.Radius ? 0.55f : 0.45f * (1f - (distance - zone.Radius) / RecoveryRange) + 0.1f;
            handle.DrawCircle(center, zone.Radius, FSPalette.Ok.WithAlpha(alpha * 0.18f));
            handle.DrawCircle(center, zone.Radius, FSPalette.Ok.WithAlpha(alpha), false);
        }
    }

    private void DrawCover(DrawingHandleWorld handle, in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;

        var me = _xform!.GetMapCoordinates(local);
        var query = _entMan.EntityQueryEnumerator<FSCoverZoneComponent, TransformComponent>();
        while (query.MoveNext(out _, out var zone, out var xform))
        {
            if (xform.MapID != args.MapId || xform.MapID != me.MapId)
                continue;

            var center = _xform.GetWorldPosition(xform);
            var distance = (center - me.Position).Length();
            if (distance > CoverRange)
                continue;

            var size = zone.Radius * 2f + 1f;
            var alpha = 0.45f * (1f - distance / CoverRange) + 0.1f;
            var grid = xform.GridUid is { } gridUid ? _xform.GetWorldRotation(gridUid) : Angle.Zero;
            handle.DrawRect(new Box2Rotated(Box2.CenteredAround(center, new Vector2(size)), grid, center), CoverColor.WithAlpha(alpha), false);
        }
    }

    private void DrawLowPower(DrawingHandleWorld handle, in OverlayDrawArgs args, TimeSpan now)
    {
        var pulse = 0.55f + 0.45f * MathF.Abs(MathF.Sin((float) now.TotalSeconds * 3f));
        var icon = _sprite!.Frame0(LowPowerIcon);
        var query = _entMan.EntityQueryEnumerator<FSMechLowPowerComponent, MechComponent, TransformComponent>();
        while (query.MoveNext(out _, out var low, out var mech, out var xform))
        {
            if (!low.Active || mech.Broken || xform.MapID != args.MapId)
                continue;

            var at = _xform!.GetWorldPosition(xform) + new Vector2(0f, LowPowerIconHeight);
            handle.DrawTextureRect(icon, Box2.CenteredAround(at, new Vector2(LowPowerIconSize)), AlarmColor.WithAlpha(pulse));
        }
    }

    private void DrawSentryLines(DrawingHandleWorld handle, in OverlayDrawArgs args)
    {
        var query = _entMan.EntityQueryEnumerator<FSSentryTurretComponent, AppearanceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sentry, out var appearance, out var xform))
        {
            if (xform.MapID != args.MapId
                || !_appearance!.TryGetData<bool>(uid, FSSentryTurretVisuals.Firing, out var firing, appearance)
                || !firing
                || !_appearance.TryGetData<double>(uid, FSSentryTurretVisuals.Angle, out var theta, appearance))
                continue;

            var dir = (new Angle(theta) - Angle.FromDegrees(sentry.SpriteAngleOffset)).ToWorldVec();
            var start = _xform!.GetWorldPosition(xform);
            handle.DrawLine(start, start + dir * sentry.Range, LaserColor.WithAlpha(0.3f));
        }
    }
}
