// World-space flair that is also information: build holograms filling in, the planted Durand's frontal shield,
// the emplacement's cover zone, and a faint line from each sentry to what it's shooting at.
using System.Numerics;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Effects;
using Content.Shared._FinalStand.Mech;
using Content.Shared.RCD;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

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
    private const float ShieldRadius = 1.25f;
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
    }

    private void DrawHolograms(DrawingHandleWorld handle, in OverlayDrawArgs args, TimeSpan now)
    {
        foreach (var holo in _flair!.Holograms)
        {
            if (!_proto.TryIndex<RCDPrototype>(holo.Piece, out var piece) || piece.Sprite is not { } spec)
                continue;

            var map = _xform!.ToMapCoordinates(holo.Coords);
            if (map.MapId != args.MapId)
                continue;

            var total = (float) (holo.End - holo.Start).TotalSeconds;
            var progress = total > 0f ? Math.Clamp((float) (now - holo.Start).TotalSeconds / total, 0f, 1f) : 1f;
            var box = Box2.CenteredAround(map.Position, Vector2.One);
            var rotated = new Box2Rotated(box, holo.Rotation, map.Position);
            handle.DrawTextureRect(_sprite!.Frame0(spec), rotated, HoloColor.WithAlpha(0.2f + 0.5f * progress));

            var y = box.Bottom + box.Height * progress;
            handle.DrawLine(new Vector2(box.Left, y), new Vector2(box.Right, y), HoloColor.WithAlpha(0.9f));
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
            handle.DrawRect(Box2.CenteredAround(center, new Vector2(size)), CoverColor.WithAlpha(alpha), false);
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
