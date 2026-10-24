using System.Numerics;
using Content.Shared._FinalStand.Particles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
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

    public const int MaxParticles = 1500;

    public struct Particle
    {
        public MapId Map;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size0;
        public float Size1;
        public float Rotation;
        public float Spin;
        public Color Color;
        public FSParticleEmitterPrototype Emitter;
        public Texture[] Frames;
    }

    private sealed class Attachment
    {
        public EntityUid Entity;
        public FSParticleEmitterPrototype Emitter = default!;
        public Color Tint;
        public float RateScale = 1f;
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

    public void SetTint(int handle, Color tint)
    {
        if (_attachments.TryGetValue(handle, out var attachment))
            attachment.Tint = tint;
    }

    public void Detach(int handle) => _attachments.Remove(handle);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var (handle, attachment) in _attachments)
        {
            if (TerminatingOrDeleted(attachment.Entity))
            {
                _deadHandles.Add(handle);
                continue;
            }

            if (attachment.Emitter.AlignToHeading && !TrackHeading(attachment))
                continue;

            attachment.Owed += attachment.Emitter.Rate * attachment.RateScale * frameTime;
            if (attachment.Owed < 1f)
                continue;

            var at = _xform.GetMapCoordinates(attachment.Entity);
            while (attachment.Owed >= 1f)
            {
                attachment.Owed -= 1f;
                Spawn(attachment.Emitter, at.MapId, at.Position, attachment.Tint, attachment.Heading);
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
            p.Rotation += p.Spin * dt;
            i++;
        }
    }

    private void Spawn(FSParticleEmitterPrototype emitter, MapId map, Vector2 origin, Color tint, float? rotation = null)
    {
        if (ParticleCount >= MaxParticles || GetFrames(emitter) is not { } frames)
            return;

        var angle = MathHelper.DegreesToRadians(emitter.Direction + _random.NextFloat(-emitter.Spread, emitter.Spread) / 2f);
        var dir = new Vector2(MathF.Cos((float) angle), MathF.Sin((float) angle));
        var speed = _random.NextFloat(emitter.Speed.X, MathF.Max(emitter.Speed.X, emitter.Speed.Y));
        var offset = emitter.SpawnRadius > 0f
            ? _random.NextAngle().ToVec() * emitter.SpawnRadius * MathF.Sqrt(_random.NextFloat())
            : Vector2.Zero;
        var jitter = 1f + _random.NextFloat(-emitter.SizeJitter, emitter.SizeJitter);

        Particles[ParticleCount++] = new Particle
        {
            Map = map,
            Position = origin + emitter.Offset + offset,
            Velocity = dir * speed,
            Life = _random.NextFloat(emitter.Lifetime.X, MathF.Max(emitter.Lifetime.X, emitter.Lifetime.Y)),
            Size0 = emitter.Size.X * jitter,
            Size1 = emitter.Size.Y * jitter,
            Rotation = rotation ?? _random.NextFloat(0f, MathF.Tau),
            Spin = MathHelper.DegreesToRadians(_random.NextFloat(emitter.Spin.X, MathF.Max(emitter.Spin.X, emitter.Spin.Y))),
            Color = emitter.Tinted ? Multiply(emitter.Color, tint) : emitter.Color,
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
