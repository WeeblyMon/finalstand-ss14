using System.Numerics;
using Content.Shared._FinalStand.Particles;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client._FinalStand.Particles;

// Pooled 2D particles: bursts at a point, or emitters that follow an entity. One additive draw pass.
public sealed partial class FSParticleSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private RayCastSystem _rayCast = default!;

    public const int MaxParticles = 1500;

    // Emitters this far outside the view stop spawning; what they already made still finishes.
    private const float EmitMargin = 8f;
    // How far short of a wall a stopped particle's centre sits.
    private const float WallMargin = 0.2f;

    private static readonly QueryFilter WallFilter = new() { MaskBits = (int) CollisionGroup.Impassable };

    public struct Particle
    {
        public MapId Map;
        public Vector2 Position;
        public Vector2 Origin;
        // Distance from Origin where a wall stops it; infinite when nothing does.
        public float Stop;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size0;
        public float Size1;
        public float Rotation;
        public float Spin;
        public Color Color;
        public Color EndColor;
        public FSParticleEmitterPrototype Emitter;
        public Texture[] Frames;
    }

    private sealed class Attachment
    {
        public EntityUid Entity;
        public FSParticleEmitterPrototype Emitter = default!;
        public Color Tint;
        public float RateScale = 1f;
        public float SpeedScale = 1f;
        public float Owed;
        public Vector2? Last;
        public float? Heading;
    }

    public readonly Particle[] Particles = new Particle[MaxParticles];
    public int ParticleCount;

    private readonly Dictionary<int, Attachment> _attachments = new();
    private readonly List<int> _deadHandles = new();
    private readonly Dictionary<string, Texture[]?> _frames = new();
    private int _nextHandle = 1;

    private FSParticleOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new FSParticleOverlay(EntityManager, this);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    public void Burst(ProtoId<FSParticleEmitterPrototype> emitterId, MapCoordinates at, Color? tint = null, float countScale = 1f)
    {
        if (!_prototypes.TryIndex(emitterId, out var emitter))
            return;

        var count = (int) MathF.Round(emitter.Burst * countScale);
        for (var i = 0; i < count; i++)
            Spawn(emitter, at.MapId, at.Position, tint ?? Color.White);
    }

    public void Burst(ProtoId<FSParticleEmitterPrototype> emitterId, EntityUid at, Color? tint = null, float countScale = 1f)
    {
        if (!Exists(at))
            return;

        Burst(emitterId, _xform.GetMapCoordinates(at), tint, countScale);
    }

    public int Attach(ProtoId<FSParticleEmitterPrototype> emitterId, EntityUid entity, Color? tint = null)
    {
        if (!_prototypes.TryIndex(emitterId, out var emitter))
            return 0;

        var handle = _nextHandle++;
        _attachments[handle] = new Attachment { Entity = entity, Emitter = emitter, Tint = tint ?? Color.White };
        return handle;
    }

    public void SetRateScale(int handle, float scale)
    {
        if (_attachments.TryGetValue(handle, out var attachment))
            attachment.RateScale = scale;
    }

    public void SetSpeedScale(int handle, float scale)
    {
        if (_attachments.TryGetValue(handle, out var attachment))
            attachment.SpeedScale = scale;
    }

    public void SetTint(int handle, Color tint)
    {
        if (_attachments.TryGetValue(handle, out var attachment))
            attachment.Tint = tint;
    }

    public void Detach(int handle) => _attachments.Remove(handle);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eye = _eye.CurrentEye.Position.MapId;
        var view = _eye.GetWorldViewport().Enlarged(EmitMargin);

        foreach (var (handle, attachment) in _attachments)
        {
            if (TerminatingOrDeleted(attachment.Entity))
            {
                _deadHandles.Add(handle);
                continue;
            }

            if (attachment.Emitter.AlignToHeading && !TrackHeading(attachment))
                continue;

            var (position, rotation) = _xform.GetWorldPositionRotation(attachment.Entity);
            var map = Transform(attachment.Entity).MapID;
            if (map != eye || !view.Contains(position))
            {
                attachment.Owed = 0f;
                continue;
            }

            attachment.Owed += attachment.Emitter.Rate * attachment.RateScale * frameTime;
            if (attachment.Owed < 1f)
                continue;

            var turn = attachment.Emitter.RotateWithEntity ? (float) rotation.Degrees : 0f;
            while (attachment.Owed >= 1f)
            {
                attachment.Owed -= 1f;
                Spawn(attachment.Emitter, map, position, attachment.Tint, attachment.Heading, turn, attachment.SpeedScale);
            }
        }

        foreach (var handle in _deadHandles)
            _attachments.Remove(handle);
        _deadHandles.Clear();

        Step(frameTime);
    }

    private bool TrackHeading(Attachment attachment)
    {
        var position = _xform.GetWorldPosition(attachment.Entity);
        if (attachment.Last is { } last && (position - last).LengthSquared() > 0.000001f)
        {
            var delta = position - last;
            attachment.Heading = MathF.Atan2(delta.Y, delta.X);
        }

        attachment.Last = position;
        return attachment.Heading != null;
    }

    private void Step(float dt)
    {
        var i = 0;
        while (i < ParticleCount)
        {
            ref var p = ref Particles[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                Particles[i] = Particles[--ParticleCount];
                continue;
            }

            var emitter = p.Emitter;
            p.Velocity += emitter.Acceleration * dt;
            if (emitter.Drag > 0f)
                p.Velocity *= MathF.Max(0f, 1f - emitter.Drag * dt);
            p.Position += p.Velocity * dt;
            if (float.IsFinite(p.Stop))
            {
                var travelled = p.Position - p.Origin;
                if (travelled.LengthSquared() > p.Stop * p.Stop)
                {
                    p.Position = p.Origin + Vector2.Normalize(travelled) * p.Stop;
                    p.Velocity = Vector2.Zero;
                }
            }

            p.Rotation += p.Spin * dt;
            i++;
        }
    }

    private void Spawn(FSParticleEmitterPrototype emitter, MapId map, Vector2 origin, Color tint, float? rotation = null, float turn = 0f, float speedScale = 1f)
    {
        if (ParticleCount >= MaxParticles || GetFrames(emitter) is not { } frames)
            return;

        var angle = MathHelper.DegreesToRadians(emitter.Direction + turn + _random.NextFloat(-emitter.Spread, emitter.Spread) / 2f);
        var dir = new Vector2(MathF.Cos((float) angle), MathF.Sin((float) angle));
        var speed = _random.NextFloat(emitter.Speed.X, MathF.Max(emitter.Speed.X, emitter.Speed.Y)) * speedScale;
        var offset = emitter.SpawnRadius > 0f
            ? _random.NextAngle().ToVec() * emitter.SpawnRadius * MathF.Sqrt(_random.NextFloat())
            : Vector2.Zero;
        var jitter = 1f + _random.NextFloat(-emitter.SizeJitter, emitter.SizeJitter);
        var life = _random.NextFloat(emitter.Lifetime.X, MathF.Max(emitter.Lifetime.X, emitter.Lifetime.Y));
        var start = origin + emitter.Offset + offset;
        var stop = float.PositiveInfinity;

        // One ray at birth decides where it stops, so a particle can't drift through a wall and nothing has to
        // react to it touching one. Cast from the emitter itself, which can't be inside the wall.
        if (emitter.StopAtWalls)
        {
            var length = emitter.ForwardOffset + speed * life;
            var hit = _rayCast.CastRayClosest(map, start, dir * length, WallFilter);
            if (hit.Hit)
                stop = MathF.Max(0f, hit.Results[0].Fraction * length - emitter.ForwardOffset - WallMargin);
        }

        start += dir * emitter.ForwardOffset;

        var color = emitter.Tinted ? Multiply(emitter.Color, tint) : emitter.Color;
        var endColor = emitter.EndColor is not { } end ? color
            : emitter.Tinted ? Multiply(end, tint) : end;

        Particles[ParticleCount++] = new Particle
        {
            Map = map,
            Position = start,
            Origin = start,
            Stop = stop,
            Velocity = dir * speed,
            Life = life,
            Size0 = emitter.Size.X * jitter,
            Size1 = emitter.Size.Y * jitter,
            Rotation = rotation ?? _random.NextFloat(0f, MathF.Tau),
            Spin = MathHelper.DegreesToRadians(_random.NextFloat(emitter.Spin.X, MathF.Max(emitter.Spin.X, emitter.Spin.Y))),
            Color = color,
            EndColor = endColor,
            Emitter = emitter,
            Frames = frames,
        };
    }

    private static Color Multiply(Color a, Color b) => new(a.R * b.R, a.G * b.G, a.B * b.B, a.A * b.A);

    private Texture[]? GetFrames(FSParticleEmitterPrototype emitter)
    {
        if (_frames.TryGetValue(emitter.ID, out var cached))
            return cached;

        Texture[]? frames = null;
        if (emitter.Animation is { } animation)
            frames = _sprite.GetState(animation).GetFrames(RsiDirection.South);
        else if (emitter.Texture is { } path && _resources.TryGetResource<TextureResource>(path, out var resource))
            frames = [resource.Texture];
        else
            Log.Error($"Particle emitter '{emitter.ID}' has no texture or animation");

        _frames[emitter.ID] = frames;
        return frames;
    }
}
