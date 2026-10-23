using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Particles;

[RegisterComponent]
public sealed partial class FSParticleTrailComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<FSParticleEmitterPrototype>> Emitters = new();
}
