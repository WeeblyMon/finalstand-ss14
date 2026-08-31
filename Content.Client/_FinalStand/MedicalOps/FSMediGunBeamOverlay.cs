using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

// A sagging ribbon rather than a straight pointer, so the beam reads as a stream of something
// rather than a targeting laser.
public sealed class FSMediGunBeamOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const int Segments = 24;
    private const float Width = 0.11f;

    // Fraction of the span the middle droops by, so short links stay taut and long ones hang.
    private const float Sag = 0.16f;

    // Slow travelling wobble, so a held beam is never perfectly still.
    private const float WobbleAmplitude = 0.05f;
    private const float WobbleSpeed = 3.2f;

    private readonly List<Vector2> _verts = new();

    public FSMediGunBeamOverlay(IEntityManager entManager, IGameTiming timing)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var time = (float)_timing.CurTime.TotalSeconds;

        // Overlays share the handle and the health bars leave a transform on it, so world
        // coordinates are only correct once this is reset.
        handle.SetTransform(Matrix3x2.Identity);

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

            if (!args.WorldAABB.Enlarged(2f).Contains(start) && !args.WorldAABB.Enlarged(2f).Contains(end))
                continue;

            BuildRibbon(start, end, time);
            if (_verts.Count >= 3)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _verts, healed.BeamColor);
        }
    }

    private void BuildRibbon(Vector2 start, Vector2 end, float time)
    {
        _verts.Clear();

        var delta = end - start;
        var span = delta.Length();
        if (span <= 0.01f)
            return;

        // Control point pulled downward, plus a wobble across the beam's own axis.
        var perpendicular = new Vector2(-delta.Y, delta.X) / span;
        var wobble = MathF.Sin(time * WobbleSpeed) * WobbleAmplitude * span;
        var control = start + delta * 0.5f + new Vector2(0f, -span * Sag) + perpendicular * wobble;

        Vector2? previousLeft = null;
        Vector2? previousRight = null;

        for (var i = 0; i <= Segments; i++)
        {
            var t = (float)i / Segments;
            var point = Bezier(start, control, end, t);
            var tangent = BezierTangent(start, control, end, t);

            var length = tangent.Length();
            if (length <= 0.0001f)
                continue;

            var normal = new Vector2(-tangent.Y, tangent.X) / length;

            // Tapered at both ends so it looks like it is emitted rather than cut off.
            var taper = MathF.Sin(t * MathF.PI);
            var halfWidth = Width * (0.35f + 0.65f * taper) * 0.5f;

            var left = point + normal * halfWidth;
            var right = point - normal * halfWidth;

            if (previousLeft is { } pl && previousRight is { } pr)
            {
                _verts.Add(pl);
                _verts.Add(pr);
                _verts.Add(left);

                _verts.Add(pr);
                _verts.Add(right);
                _verts.Add(left);
            }

            previousLeft = left;
            previousRight = right;
        }
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
