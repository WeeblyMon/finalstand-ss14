using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

// Goob's animated beam texture, bent along a sagging curve rather than drawn as a straight line.
public sealed class FSMediGunBeamOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;

    private static readonly SpriteSpecifier BeamSprite = new SpriteSpecifier.Rsi(
        new ResPath("/Textures/_Goobstation/Objects/Specific/Medical/medigun.rsi"), "beam");

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    // One texture repeat per this many metres, so the beam reads at any range.
    private const float TileLength = 0.5f;
    private const int SegmentsPerTile = 4;
    private const int MaxSegments = 64;

    private const float Width = 0.32f;
    private const float Sag = 0.16f;
    private const float WobbleAmplitude = 0.05f;
    private const float WobbleSpeed = 3.2f;

    private readonly List<DrawVertexUV2D> _verts = new();

    public FSMediGunBeamOverlay(IEntityManager entManager, IGameTiming timing)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var time = (float)_timing.CurTime.TotalSeconds;

        // Overlays share the handle and the health bars leave a transform on it.
        handle.SetTransform(Matrix3x2.Identity);

        var texture = _sprite.GetFrame(BeamSprite, _timing.RealTime);

        var query = _entManager.EntityQueryEnumerator<FSMediGunHealedComponent>();
        while (query.MoveNext(out var patient, out var healed))
        {
            if (!_entManager.TryGetComponent(healed.Source, out FSMediGunComponent? gun)
                || gun.ParentEntity is not { } medic
                || !_entManager.EntityExists(medic)
                || !_entManager.EntityExists(patient))
                continue;

            if (!_entManager.TryGetComponent(medic, out TransformComponent? medicXform)
                || !_entManager.TryGetComponent(patient, out TransformComponent? patientXform)
                || medicXform.MapID != args.MapId
                || patientXform.MapID != args.MapId)
                continue;

            var start = _transform.GetWorldPosition(medicXform);
            var end = _transform.GetWorldPosition(patientXform);

            var bounds = args.WorldAABB.Enlarged(3f);
            if (!bounds.Contains(start) && !bounds.Contains(end))
                continue;

            BuildRibbon(start, end, time);
            if (_verts.Count >= 3)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, texture, _verts.ToArray(), healed.BeamColor);
        }
    }

    private void BuildRibbon(Vector2 start, Vector2 end, float time)
    {
        _verts.Clear();

        var delta = end - start;
        var span = delta.Length();
        if (span <= 0.01f)
            return;

        var perpendicular = new Vector2(-delta.Y, delta.X) / span;
        var wobble = MathF.Sin(time * WobbleSpeed) * WobbleAmplitude * span;
        var control = start + delta * 0.5f + new Vector2(0f, -span * Sag) + perpendicular * wobble;

        var tiles = MathF.Max(1f, span / TileLength);
        var segments = Math.Clamp((int)(tiles * SegmentsPerTile), 4, MaxSegments);

        Vector2 previousLeft = default, previousRight = default;
        float previousU = 0f;
        var first = true;

        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var point = Bezier(start, control, end, t);
            var tangent = BezierTangent(start, control, end, t);

            var length = tangent.Length();
            if (length <= 0.0001f)
                continue;

            var normal = new Vector2(-tangent.Y, tangent.X) / length;
            var half = Width * 0.5f;

            var left = point + normal * half;
            var right = point - normal * half;

            // Scrolls along the beam so the nanites look like they are travelling.
            var u = t * tiles - time * 1.5f;

            if (!first)
            {
                Add(previousLeft, previousU, 0f);
                Add(previousRight, previousU, 1f);
                Add(left, u, 0f);

                Add(previousRight, previousU, 1f);
                Add(right, u, 1f);
                Add(left, u, 0f);
            }

            previousLeft = left;
            previousRight = right;
            previousU = u;
            first = false;
        }
    }

    private void Add(Vector2 position, float u, float v)
    {
        _verts.Add(new DrawVertexUV2D(position, new Vector2(u, v)));
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        var inv = 1f - t;
        return a * (inv * inv) + b * (2f * inv * t) + c * (t * t);
    }

    private static Vector2 BezierTangent(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        return (b - a) * (2f * (1f - t)) + (c - b) * (2f * t);
    }
}
