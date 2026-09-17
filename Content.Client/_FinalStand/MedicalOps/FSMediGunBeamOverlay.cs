using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Player;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSMediGunBeamOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly SharedHandsSystem _hands;
    private readonly IPlayerManager _player;

    private readonly Texture? _beam;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const int GridSize = 4;
    private const int FrameCount = GridSize * GridSize;
    private const float FrameSeconds = 0.1f;

    private const float TileLength = 0.6f;
    private const int SegmentsPerTile = 5;
    private const int MaxSegments = 80;

    private const float Width = 0.62f;

    private const float LagResponse = 7f;   // higher snaps straight faster
    private const float LagAmplify = 1.6f;  // exaggerates the trail so the whip reads
    private const float MaxLag = 1.1f;      // metres, so a teleport cannot fling the arc away
    private const double LagForgetSeconds = 3d;

    private const float ParticlesPerMetre = 1.6f;
    private const int MaxParticles = 14;
    private const float ParticleSize = 0.096f;
    private const float ParticleDrift = 0.35f;
    private const float ParticleOrbit = 0.13f;
    private const float ParticleOrbitSpeed = 2.4f;

    private readonly List<DrawVertexUV2D> _verts = new();
    private DrawVertexUV2D[] _vertBuffer = new DrawVertexUV2D[64];

    private readonly record struct LagState(Vector2 Mid, TimeSpan LastSeen);

    private readonly Dictionary<EntityUid, LagState> _lag = new();
    private readonly List<EntityUid> _stale = new();

    public FSMediGunBeamOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache cache)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();
        _hands = _entManager.System<SharedHandsSystem>();
        _player = IoCManager.Resolve<IPlayerManager>();

        _beam = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Effects/medigun_beam.png");
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_beam == null)
            return;

        var handle = args.WorldHandle;
        var time = (float)_timing.CurTime.TotalSeconds;
        var dt = (float)_timing.FrameTime.TotalSeconds;

        handle.SetTransform(Matrix3x2.Identity);

        PruneLag();

        var frame = (int)(_timing.RealTime.TotalSeconds / FrameSeconds) % FrameCount;
        var col = frame % GridSize;
        var row = frame / GridSize;
        var cell = 1f / GridSize;
        var uMin = col * cell;
        var vMin = row * cell;

        var localGun = LocalMediGun();
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

            var control = UpdateControlPoint(patient, start, end, dt);

            BuildRibbon(start, control, end, uMin, vMin, cell);

            if (_verts.Count >= 3)
            {
                if (_vertBuffer.Length < _verts.Count)
                    _vertBuffer = new DrawVertexUV2D[_verts.Count];

                _verts.CopyTo(_vertBuffer);

                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _beam,
                    new ReadOnlySpan<DrawVertexUV2D>(_vertBuffer, 0, _verts.Count), healed.BeamColor);
            }

            DrawParticles(handle, start, control, end, time, healed.BeamColor);

            // Client-side ring so a medic can tell their own patient apart from someone else's.
            // Drawn only for the local medigun, so the world does not fill with other people's rings.
            if (localGun != null && healed.Source == localGun)
                DrawTargetRing(handle, end, time, healed.BeamColor);
        }
    }

    private const float RingRadius = 0.45f;
    private const int RingSegments = 20;

    private EntityUid? LocalMediGun()
    {
        if (_player.LocalEntity is not { } local
            || !_entManager.TryGetComponent(local, out HandsComponent? hands))
        {
            return null;
        }

        foreach (var held in _hands.EnumerateHeld((local, hands)))
        {
            if (_entManager.HasComponent<FSMediGunComponent>(held))
                return held;
        }

        return null;
    }

    private static void DrawTargetRing(DrawingHandleWorld handle, Vector2 centre, float time, Color colour)
    {
        var pulse = 1f + MathF.Sin(time * 4f) * 0.06f;
        var radius = RingRadius * pulse;

        for (var i = 0; i < RingSegments; i++)
        {
            var a0 = MathF.Tau * i / RingSegments;
            var a1 = MathF.Tau * (i + 1) / RingSegments;

            handle.DrawLine(
                centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius,
                centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius,
                colour.WithAlpha(0.75f));
        }
    }

    private void BuildRibbon(Vector2 start, Vector2 control, Vector2 end, float uMin, float vMin, float cell)
    {
        _verts.Clear();

        var span = (end - start).Length();
        if (span <= 0.01f)
            return;

        var tiles = Math.Max(1, (int)MathF.Round(span / TileLength));
        var segments = Math.Min(tiles * SegmentsPerTile, MaxSegments);

        for (var seg = 0; seg < segments; seg++)
        {
            var t0 = (float)seg / segments;
            var t1 = (float)(seg + 1) / segments;

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

    private void DrawParticles(DrawingHandleWorld handle, Vector2 start, Vector2 control, Vector2 end, float time, Color tint)
    {
        var span = (end - start).Length();
        if (span <= 0.01f)
            return;

        var count = Math.Clamp((int)(span * ParticlesPerMetre), 3, MaxParticles);
        var colour = Color.InterpolateBetween(tint, Color.White, 0.55f);

        for (var i = 0; i < count; i++)
        {
            var phase = (float)i / count;

            var t = (phase + time * ParticleDrift) % 1f;

            var point = Bezier(start, control, end, t);
            var tangent = BezierTangent(start, control, end, t);
            var length = tangent.Length();
            if (length <= 0.0001f)
                continue;

            var normal = new Vector2(-tangent.Y, tangent.X) / length;
            var orbit = MathF.Sin(time * ParticleOrbitSpeed + phase * MathF.Tau) * ParticleOrbit;
            var centre = point + normal * orbit;

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

    private Vector2 UpdateControlPoint(EntityUid patient, Vector2 start, Vector2 end, float dt)
    {
        var trueMid = (start + end) * 0.5f;

        if (!_lag.TryGetValue(patient, out var state))
            state = new LagState(trueMid, _timing.RealTime);

        var blend = 1f - MathF.Exp(-LagResponse * dt);
        var mid = Vector2.Lerp(state.Mid, trueMid, blend);

        _lag[patient] = new LagState(mid, _timing.RealTime);

        var offset = (mid - trueMid) * LagAmplify;
        var distance = offset.Length();
        if (distance > MaxLag)
            offset = offset / distance * MaxLag;

        return trueMid + offset;
    }

    private void PruneLag()
    {
        if (_lag.Count == 0)
            return;

        var now = _timing.RealTime;
        foreach (var (uid, state) in _lag)
        {
            if ((now - state.LastSeen).TotalSeconds > LagForgetSeconds)
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
            _lag.Remove(uid);

        _stale.Clear();
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
