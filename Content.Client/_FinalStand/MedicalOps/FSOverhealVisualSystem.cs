using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.MedicalOps;

namespace Content.Client._FinalStand.MedicalOps;

// TF2 overheal: crosses rise off the patient in proportion to their shield, and shatter when it breaks.
public sealed partial class FSOverhealVisualSystem : EntitySystem
{
    [Dependency] private FSParticleSystem _particles = default!;

    private readonly Dictionary<EntityUid, int> _emitters = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSOverhealComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<FSOverhealBrokenEvent>(OnBroken);
    }

    private void OnShutdown(Entity<FSOverhealComponent> ent, ref ComponentShutdown args)
    {
        if (_emitters.Remove(ent, out var handle))
            _particles.Detach(handle);
    }

    private void OnBroken(FSOverhealBrokenEvent ev)
    {
        var patient = GetEntity(ev.Patient);
        var colour = TryComp<FSOverhealComponent>(patient, out var shield) ? shield.SourceColor : Color.FromHex("#3FD7E0");
        _particles.Burst("FSOverhealShatter", patient, colour);
        _particles.Burst("FSOverhealBreakFlash", patient, colour);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<FSOverhealComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (!_emitters.TryGetValue(uid, out var handle))
                _emitters[uid] = handle = _particles.Attach("FSOverhealCross", uid, shield.SourceColor);

            var fraction = shield.Max > 0f ? Math.Clamp(shield.Amount / shield.Max, 0f, 1f) : 0f;
            _particles.SetRateScale(handle, 0.15f + fraction * 0.85f);
            _particles.SetTint(handle, shield.SourceColor);
        }
    }
}
