// World-space flair that is also information: build holograms filling in, the planted Durand's frontal shield,
// the emplacement's cover zone, a faint line from each sentry to what it's shooting at, and a mech's low-power blink.
using System.Numerics;
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
    private const int ShieldSegments = 16;
    private const float CoverRange = 8f;

    private SharedTransformSystem? _xform;
    private SpriteSystem? _sprite;
    private SharedAppearanceSystem? _appearance;
    private FSFlairClientSystem? _flair;

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

    private void DrawShields(DrawingHandleWorld handle, in OverlayDrawArgs args, float pulse)
    {
        var query = _entMan.EntityQueryEnumerator<FSSiegeModeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var siege, out var xform))
        {
            if (!siege.Planted || xform.MapID != args.MapId)
                continue;

            var center = _xform!.GetWorldPosition(xform);
            var facing = _xform.GetWorldRotation(xform);
            var half = Angle.FromDegrees(siege.FrontalArc / 2f);
            var points = new List<Vector2> { center };
            for (var i = 0; i <= ShieldSegments; i++)
            {
                var angle = facing - half + (half * 2) * (i / (float) ShieldSegments);
                points.Add(center + angle.ToWorldVec() * ShieldRadius);
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points.ToArray(), ShieldColor.WithAlpha(0.12f + 0.06f * pulse));
            for (var i = 1; i < points.Count - 1; i++)
                handle.DrawLine(points[i], points[i + 1], ShieldColor.WithAlpha(0.55f + 0.25f * pulse));
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
        var query = _entMan.EntityQueryEnumerator<FSCombatMechComponent, MechComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var mech, out var xform))
        {
            if (!mech.CriticalPowerState || mech.Broken || xform.MapID != args.MapId)
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
