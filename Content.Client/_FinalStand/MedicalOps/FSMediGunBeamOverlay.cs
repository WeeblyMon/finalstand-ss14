using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

// Goob's animated beam sheet, bent along a sagging curve instead of drawn as a straight line.
// The sheet is loaded as a plain texture rather than an RSI state: DrawPrimitives rejects atlas
// sub-textures, and a raw PNG read from inside a .rsi does not survive packaging.
public sealed class FSMediGunBeamOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    private readonly Texture? _beam;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    // The sheet is a 4x4 grid of frames.
    private const int GridSize = 4;
    private const int FrameCount = GridSize * GridSize;
    private const float FrameSeconds = 0.1f;

    // One full frame of the texture per this many metres.
    private const float TileLength = 0.6f;
    private const int SegmentsPerTile = 5;
    private const int MaxSegments = 80;

    // The beam art only occupies the middle of each frame, so the quad has to be wider than the
    // apparent beam.
    private const float Width = 0.62f;

    // Droop and sway are capped in metres rather than scaling with distance, or a long beam bows
    // into an arc instead of hanging slightly.
    private const float Sag = 0.09f;
    private const float MaxSag = 0.30f;
    private const float WobbleAmplitude = 0.035f;
    private const float MaxWobble = 0.09f;
    private const float WobbleSpeed = 3.2f;

    private const float ParticlesPerMetre = 1.6f;
    private const int MaxParticles = 14;
    private const float ParticleSize = 0.16f;
    private const float ParticleDrift = 0.21f;
    private const float ParticleOrbit = 0.13f;
    private const float ParticleOrbitSpeed = 2.4f;

    private readonly List<DrawVertexUV2D> _verts = new();

    public FSMediGunBeamOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache cache)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();

        try
        {
            _beam = cache.GetResource<TextureResource>(
                new ResPath("/Textures/_FinalStand/Effects/medigun_beam.png")).Texture;
        }
        catch
        {
            _beam = null;
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_beam == null)
            return;

        var handle = args.WorldHandle;
        var time = (float)_timing.CurTime.TotalSeconds;

        // Overlays share the handle and the health bars leave a transform on it.
        handle.SetTransform(Matrix3x2.Identity);

        var frame = (int)(_timing.RealTime.TotalSeconds / FrameSeconds) % FrameCount;
        var col = frame % GridSize;
        var row = frame / GridSize;
        var cell = 1f / GridSize;
        var uMin = col * cell;
        var vMin = row * cell;

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

            BuildRibbon(start, end, time, uMin, vMin, cell);

            if (_verts.Count >= 3)
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _beam, _verts.ToArray(), healed.BeamColor);

            DrawParticles(handle, start, end, time, healed.BeamColor);
        }
    }

    private void BuildRibbon(Vector2 start, Vector2 end, float time, float uMin, float vMin, float cell)
    {
        _verts.Clear();

        var delta = end - start;
        var span = delta.Length();
        if (span <= 0.01f)
            return;

        var control = GetControlPoint(start, delta, span, time);

        var tiles = Math.Max(1, (int)MathF.Round(span / TileLength));
        var segments = Math.Min(tiles * SegmentsPerTile, MaxSegments);

        for (var seg = 0; seg < segments; seg++)
        {
            var t0 = (float)seg / segments;
            var t1 = (float)(seg + 1) / segments;

            // The beam is drawn vertically inside each frame, so V runs along the beam's length
            // and U across its width. Restarting the V span at every tile boundary keeps the frame
            // from smearing backwards across the seam.
            var k = seg % SegmentsPerTile;
            var v0 = vMin + cell * ((float)k / SegmentsPerTile);
            var v1 = vMin + cell * ((float)(k + 1) / SegmentsPerTile);

            if (!TryEdge(start, control, end, t0, out var l0, out var r0)
                || !TryEdge(start, control, end, t1, out var l1, out var r1))
                continue;

            Add(l0, uMin, v0);
            Add(r0, uMin + cell, v0);
            Add(l1, uMin, v1);

            Add(r0, uMin + cell, v0);
            Add(r1, uMin + cell, v1);
            Add(l1, uMin, v1);
        }
    }

    // Little crosses riding the beam toward the patient, so healing reads as something being
    // delivered rather than a light being shone.
    private void DrawParticles(DrawingHandleWorld handle, Vector2 start, Vector2 end, float time, Color tint)
    {
        var delta = end - start;
        var span = delta.Length();
        if (span <= 0.01f)
            return;

        var control = GetControlPoint(start, delta, span, time);

        var count = Math.Clamp((int)(span * ParticlesPerMetre), 3, MaxParticles);
        var colour = Color.InterpolateBetween(tint, Color.White, 0.55f);

        for (var i = 0; i < count; i++)
        {
            var phase = (float)i / count;

            // Each cross drifts from medic to patient and restarts.
            var t = (phase + time * ParticleDrift) % 1f;

            var point = Bezier(start, control, end, t);
            var tangent = BezierTangent(start, control, end, t);
            var length = tangent.Length();
            if (length <= 0.0001f)
                continue;

            var normal = new Vector2(-tangent.Y, tangent.X) / length;
            var orbit = MathF.Sin(time * ParticleOrbitSpeed + phase * MathF.Tau) * ParticleOrbit;
            var centre = point + normal * orbit;

            // Fade in and out at the ends so they do not pop into existence.
            var fade = MathF.Sin(t * MathF.PI);
            var size = ParticleSize * (0.6f + 0.4f * fade);
            var arm = size * 0.5f;
            var thickness = size * 0.34f;
            var half = thickness * 0.5f;

            var faded = colour.WithAlpha(fade * 0.9f);

            handle.DrawRect(new Box2(centre.X - arm, centre.Y - half, centre.X + arm, centre.Y + half), faded);
            handle.DrawRect(new Box2(centre.X - half, centre.Y - arm, centre.X + half, centre.Y + arm), faded);
        }
    }

    private static bool TryEdge(Vector2 a, Vector2 b, Vector2 c, float t, out Vector2 left, out Vector2 right)
    {
        left = default;
        right = default;

        var point = Bezier(a, b, c, t);
        var tangent = BezierTangent(a, b, c, t);
        var length = tangent.Length();

        if (length <= 0.0001f)
            return false;

        var normal = new Vector2(-tangent.Y, tangent.X) / length;
        var half = Width * 0.5f;

        left = point + normal * half;
        right = point - normal * half;
        return true;
    }

    private void Add(Vector2 position, float u, float v)
    {
        _verts.Add(new DrawVertexUV2D(position, new Vector2(u, v)));
    }

    // Shared by the ribbon and the particles, so the crosses always ride the curve they are drawn on.
    private static Vector2 GetControlPoint(Vector2 start, Vector2 delta, float span, float time)
    {
        var perpendicular = new Vector2(-delta.Y, delta.X) / span;
        var sag = MathF.Min(span * Sag, MaxSag);
        var wobble = MathF.Sin(time * WobbleSpeed) * MathF.Min(span * WobbleAmplitude, MaxWobble);

        return start + delta * 0.5f + new Vector2(0f, -sag) + perpendicular * wobble;
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
