using System.Numerics;
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Particles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Visuals;

// A firing flamethrower zombie's fire, made here rather than sent: the server only says whether it is firing, and
// the cone follows the zombie's facing, which the server turns to aim.
public sealed partial class FSFlamethrowerVisualsSystem : EntitySystem
{
    [Dependency] private FSParticleSystem _particles = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> FlameEmitter = "FSFlamethrowerFlame";
    private static readonly EntProtoId GlowProto = "FSFlamethrowerGlow";

    private readonly Dictionary<EntityUid, (int Emitter, EntityUid Glow)> _flames = new();
    // Firing changes arrive while entity states are being applied, when the zombie may not exist yet; they are
    // acted on in the next frame update.
    private readonly HashSet<EntityUid> _changed = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSFlamethrowerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _flames.Clear();
        _changed.Clear();
    }

    private void OnShutdown(EntityUid uid, FSFlamethrowerComponent comp, ComponentShutdown args)
    {
        _changed.Remove(uid);
        Stop(uid);
    }

    public bool IsBurning(EntityUid uid) => _flames.ContainsKey(uid);

    public void Refresh(EntityUid uid)
    {
        _changed.Add(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var uid in _changed)
        {
            if (TryComp<FSFlamethrowerComponent>(uid, out var comp) && comp.IsFiring)
                Start((uid, comp));
            else
                Stop(uid);
        }

        _changed.Clear();
    }

    private void Start(Entity<FSFlamethrowerComponent> ent)
    {
        if (_flames.ContainsKey(ent))
            return;

        var emitter = _particles.Attach(FlameEmitter, ent);
        // The emitter is tuned for one flame speed; each flamer's own speed sets how far its fire reaches.
        if (_prototypes.TryIndex(FlameEmitter, out var proto) && proto.Speed.X + proto.Speed.Y > 0f)
            _particles.SetSpeedScale(emitter, ent.Comp.FireProjectileSpeed * 2f / (proto.Speed.X + proto.Speed.Y));

        // A child of the zombie half way along the flames; its local up is the way it aims, so the light swings with it.
        var glow = Spawn(GlowProto, new EntityCoordinates(ent, new Vector2(0f, ent.Comp.FlameRange * 0.5f)));
        _flames[ent] = (emitter, glow);
    }

    private void Stop(EntityUid uid)
    {
        if (!_flames.Remove(uid, out var flame))
            return;

        _particles.Detach(flame.Emitter);
        if (!TerminatingOrDeleted(flame.Glow))
            QueueDel(flame.Glow);
    }
}
