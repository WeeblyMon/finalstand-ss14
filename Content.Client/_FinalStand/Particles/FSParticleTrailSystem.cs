using Content.Shared._FinalStand.Particles;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Particles;

public sealed partial class FSParticleTrailSystem : EntitySystem
{
    [Dependency] private FSParticleSystem _particles = default!;

    private readonly Dictionary<EntityUid, int[]> _handles = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(FSParticleSystem));
        SubscribeLocalEvent<FSParticleTrailComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSParticleTrailComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<FSParticleTrailComponent> ent, ref ComponentStartup args)
    {
        var handles = new int[ent.Comp.Emitters.Count];
        for (var i = 0; i < handles.Length; i++)
            handles[i] = _particles.Attach(ent.Comp.Emitters[i], ent);

        _handles[ent] = handles;
    }

    private void OnShutdown(Entity<FSParticleTrailComponent> ent, ref ComponentShutdown args)
    {
        if (!_handles.Remove(ent, out var handles))
            return;

        foreach (var handle in handles)
            _particles.Detach(handle);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var (uid, handles) in _handles)
        {
            var scale = TryComp<SpriteComponent>(uid, out var sprite) && !sprite.Visible ? 0f : 1f;
            foreach (var handle in handles)
                _particles.SetRateScale(handle, scale);
        }
    }
}
