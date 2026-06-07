using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSFlamethrowerComponent : Component
{
    [DataField] public float FlameRange = 5f;
    [DataField] public float ConeDegrees = 20f;
    [DataField] public float AttackDuration = 3f;
    [DataField] public float AttackCooldown = 4f;
    [DataField] public float ParticleSpawnRate = 0.08f;
    [DataField] public int ParticlesPerBurst = 2;
    [DataField] public float FireProjectileSpeed = 10f;
    [DataField] public SoundSpecifier FireLoopSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Mobs/Flamethrower/fire_loop.ogg")
        {
            Params = AudioParams.Default.WithMaxDistance(8f).WithRolloffFactor(2f),
        };

    // Runtime state
    [AutoNetworkedField]
    public bool IsFiring = false;
    public Vector2 FiringDirection = Vector2.UnitX;
    public float FireAccumulator = 0f;
    public float CooldownAccumulator = 0f;
    public float ParticleAccumulator = 0f;
    public EntityUid? FireSoundEntity = null;
}
