using Content.Shared._FinalStand.Particles;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSSplashShotComponent : Component
{
    [DataField]
    public float Fraction = 0.5f;

    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public List<ProtoId<FSParticleEmitterPrototype>> Bursts = new();
}
