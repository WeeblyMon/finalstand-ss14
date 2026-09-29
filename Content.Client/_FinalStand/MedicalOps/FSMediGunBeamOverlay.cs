using System.Numerics;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSMediGunBeamOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    private readonly Texture? _beam;
    private readonly Texture? _dot;
    private readonly Texture? _cross;
    private readonly Texture? _glint;
    private readonly ShaderInstance _additive;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private const int GridSize = 4;
    private const int FrameCount = GridSize * GridSize;
    private const float FrameSeconds = 0.1f;

    private const float TileLength = 0.6f;
    private const int SegmentsPerTile = 5;
    private const int MaxSegments = 80;

    private const float Width = 0.62f;
    private const float HaloWidth = 2.2f;
    private const float UberWidth = 1.6f;

    private const int StrandCount = 2;
    private const float StrandAmplitude = 0.11f;
    private const float StrandTurnsPerTile = 1.4f;
    private const float StrandSpeed = 5f;
    private const float StrandThickness = 0.035f;

    private static readonly Color UberGold = Color.FromHex("#FFD86B");

    private const float LagResponse = 7f;
    private const float LagAmplify = 1.6f;
    private const float MaxLag = 1.1f;
    private const double LagForgetSeconds = 3d;

    private const float ParticlesPerMetre = 1.6f;
    private const int MaxParticles = 14;
    private const float ParticleSize = 0.096f;
    private const float ParticleDrift = 0.35f;
    private const float ParticleOrbit = 0.13f;
    private const float ParticleOrbitSpeed = 2.4f;

    private readonly List<DrawVertexUV2D> _verts = new();
    private DrawVertexUV2D[] _vertBuffer = new DrawVertexUV2D[64];
    private readonly Vector2[] _quad = new Vector2[6];

    private readonly record struct LagState(Vector2 Mid, TimeSpan LastSeen);

    private readonly Dictionary<(EntityUid Gun, EntityUid Patient), LagState> _lag = new();
    private readonly List<(EntityUid Gun, EntityUid Patient)> _stale = new();

    public FSMediGunBeamOverlay(IEntityManager entManager, IGameTiming timing, IResourceCache cache)
    {
        _entManager = entManager;
        _timing = timing;
        _transform = _entManager.System<SharedTransformSystem>();

        _beam = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Effects/medigun_beam.png");
        _dot = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Effects/Particles/dot.png");
        _cross = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Effects/Particles/cross.png");
        _glint = FSOverlayTextures.TryLoad(cache, "/Textures/_FinalStand/Effects/Particles/glint.png");
        _additive = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("FSAdditive").Instance();
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

        var query = _entManager.EntityQueryEnumerator<FSMediGunHealedComponent>();
        while (query.MoveNext(out var patient, out var healed))
        {
            foreach (var source in healed.Sources)
                DrawBeam(handle, _beam, args, source, patient, time, dt, uMin, vMin, cell);
        }
    }

    private void DrawBeam(DrawingHandleWorld handle, Texture beam, in OverlayDrawArgs args, EntityUid source,
        EntityUid patient, float time, float dt, float uMin, float vMin, float cell)
    {
        if (!_entManager.TryGetComponent(source, out FSMediGunComponent? gun)
            || gun.ParentEntity is not { } medic
            || !_entManager.EntityExists(medic)
            || !_entManager.EntityExists(patient))
            return;

        if (gun.BeamFromSelf)
            medic = source;

        if (!_entManager.TryGetComponent(medic, out TransformComponent? medicXform)
            || !_entManager.TryGetComponent(patient, out TransformComponent? patientXform)
            || medicXform.MapID != args.MapId
            || patientXform.MapID != args.MapId)
            return;

        var start = _transform.GetWorldPosition(medicXform);
        if (gun.BeamFromSelf)
            start.Y += FSMediDroneVisualSystem.HoverHeight;
        var end = _transform.GetWorldPosition(patientXform);

        var bounds = args.WorldAABB.Enlarged(3f);
        if (!bounds.Contains(start) && !bounds.Contains(end))
            return;

        var control = UpdateControlPoint((source, patient), start, end, dt);
        var uber = gun.UberActive;
        var width = Width * (uber ? UberWidth : 1f);

        handle.UseShader(_additive);
        DrawRibbon(handle, beam, start, control, end, width * HaloWidth, uMin, vMin, cell,
            gun.BeamColor.WithAlpha(uber ? 0.55f : 0.28f));
        handle.UseShader(null);

        DrawRibbon(handle, beam, start, control, end, width, uMin, vMin, cell, gun.BeamColor);

        handle.UseShader(_additive);
        DrawStrands(handle, start, control, end, time, Color.InterpolateBetween(gun.BeamColor, Color.White, 0.5f), uber);
        DrawPulse(handle, start, control, end, time, gun, patient);
        if (!uber && gun.Variant == FSMediGunVariant.UberCharger && gun.UberCharge >= 100f)
            DrawReadyGlints(handle, start, control, end, time);
        handle.UseShader(null);

        DrawParticles(handle, start, control, end, time, gun.BeamColor);
    }

    private void DrawRibbon(DrawingHandleWorld handle, Texture beam, Vector2 start, Vector2 control, Vector2 end,
        float width, float uMin, float vMin, float cell, Color colour)
    {
        BuildRibbon(start, control, end, width, uMin, vMin, cell);
        if (_verts.Count < 3)
            return;

        if (_vertBuffer.Length < _verts.Count)
            _vertBuffer = new DrawVertexUV2D[_verts.Count];

        _verts.CopyTo(_vertBuffer);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, beam,
            new ReadOnlySpan<DrawVertexUV2D>(_vertBuffer, 0, _verts.Count), colour);
    }

    // Two thin threads twisting around the core, like the TF2 medigun stream.
    private void DrawStrands(DrawingHandleWorld handle, Vector2 start, Vector2 control, Vector2 end, float time,
        Color colour, bool uber)
    {
        var span = (end - start).Length();
        if (span <= 0.01f)
            return;

        var steps = Math.Clamp((int) (span * 8f), 8, 96);
        var amplitude = StrandAmplitude * (uber ? 1.6f : 1f);

        for (var strand = 0; strand < StrandCount; strand++)
        {
            var phase = strand * MathF.PI;
            Vector2? previous = null;

            for (var i = 0; i <= steps; i++)
            {
                var t = (float) i / steps;
                var tangent = BezierTangent(start, control, end, t);
                var length = tangent.Length();
                if (length <= 0.0001f)
                    continue;

                var normal = new Vector2(-tangent.Y, tangent.X) / length;
                var taper = MathF.Sin(t * MathF.PI);
                var wave = MathF.Sin(t * span * StrandTurnsPerTile * MathF.Tau - time * StrandSpeed + phase);
                var point = Bezier(start, control, end, t) + normal * wave * amplitude * taper;

                if (previous is { } from)
                    DrawThickLine(handle, from, point, StrandThickness, colour.WithAlpha(0.55f * taper + 0.1f));

                previous = point;
            }
        }
    }

    // One bright packet per heal tick; it turns into a cross once the heal is feeding an overheal shield.
    private void DrawPulse(DrawingHandleWorld handle, Vector2 start, Vector2 control, Vector2 end, float time,
        FSMediGunComponent gun, EntityUid patient)
    {
        var period = MathF.Max(0.2f, gun.Frequency);
        var t = time / period % 1f;
        var point = Bezier(start, control, end, t);
        var fade = MathF.Sin(t * MathF.PI);

        var overhealing = gun.OverhealRatio > 0f
                          && _entManager.TryGetComponent(patient, out FSOverhealComponent? shield)
                          && shield.Amount < shield.Max;

        var texture = overhealing ? _cross : _dot;
        if (texture == null)
            return;

        var size = (overhealing ? 0.42f : 0.34f) * (0.7f + 0.3f * fade) * (gun.UberActive ? 1.4f : 1f);
        var colour = Color.InterpolateBetween(gun.BeamColor, Color.White, overhealing ? 0.55f : 0.35f);
        handle.DrawTextureRect(texture, Box2.CenteredAround(point, new Vector2(size, size)), colour.WithAlpha(fade));
    }

    private void DrawReadyGlints(DrawingHandleWorld handle, Vector2 start, Vector2 control, Vector2 end, float time)
    {
        if (_glint == null)
            return;

        for (var i = 0; i < 3; i++)
        {
            var t = (time * 0.6f + i / 3f) % 1f;
            var point = Bezier(start, control, end, t);
            var twinkle = 0.5f + 0.5f * MathF.Sin(time * 9f + i * 2.1f);
            var size = 0.3f + 0.15f * twinkle;
            var box = new Box2Rotated(Box2.CenteredAround(point, new Vector2(size, size)), time * 2f + i, point);
            handle.DrawTextureRect(_glint, box, UberGold.WithAlpha(MathF.Sin(t * MathF.PI) * (0.6f + 0.4f * twinkle)));
        }
    }

    private void DrawThickLine(DrawingHandleWorld handle, Vector2 a, Vector2 b, float thickness, Color colour)
    {
        var dir = b - a;
        var length = dir.Length();
        if (length <= 0.0001f)
            return;

        var normal = new Vector2(-dir.Y, dir.X) / length * (thickness / 2f);
        _quad[0] = a + normal;
        _quad[1] = a - normal;
        _quad[2] = b + normal;
        _quad[3] = a - normal;
        _quad[4] = b - normal;
        _quad[5] = b + normal;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _quad, colour);
    }

    private void BuildRibbon(Vector2 start, Vector2 control, Vector2 end, float width, float uMin, float vMin, float cell)
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

            if (!TryEdge(start, control, end, t0, width, out var l0, out var r0)
                || !TryEdge(start, control, end, t1, width, out var l1, out var r1))
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

    private static bool TryEdge(Vector2 a, Vector2 b, Vector2 c, float t, float width, out Vector2 left, out Vector2 right)
    {
        left = default;
        right = default;

        var point = Bezier(a, b, c, t);
        var tangent = BezierTangent(a, b, c, t);
        var length = tangent.Length();

        if (length <= 0.0001f)
            return false;

        var normal = new Vector2(-tangent.Y, tangent.X) / length;
        var half = width * 0.5f;

        left = point + normal * half;
        right = point - normal * half;
        return true;
    }

    private void Add(Vector2 position, float u, float v)
    {
        _verts.Add(new DrawVertexUV2D(position, new Vector2(u, v)));
    }

    private Vector2 UpdateControlPoint((EntityUid Gun, EntityUid Patient) key, Vector2 start, Vector2 end, float dt)
    {
        var trueMid = (start + end) * 0.5f;

        if (!_lag.TryGetValue(key, out var state))
            state = new LagState(trueMid, _timing.RealTime);

        var blend = 1f - MathF.Exp(-LagResponse * dt);
        var mid = Vector2.Lerp(state.Mid, trueMid, blend);

        _lag[key] = new LagState(mid, _timing.RealTime);

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
