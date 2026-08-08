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
    [DataField] public float WindupDuration = 0.6f;
    [DataField] public float TrackingRotationSpeed = 3.0f; // radians per second

    // Wider than FlameRange so a target that steps out of the flames is still followed.
    [DataField] public float TrackingRange = 12f;
    [DataField] public SoundSpecifier FireLoopSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Mobs/Flamethrower/fire_loop.ogg")
        {
            Params = AudioParams.Default.WithLoop(true).WithMaxDistance(11f).WithRolloffFactor(0.5f),
        };

    // Runtime state
    [AutoNetworkedField] public bool IsFiring = false;
    [AutoNetworkedField] public bool IsWindingUp = false;
    public float WindupAccumulator = 0f;
    public Vector2 FiringDirection = Vector2.UnitX;
    public float FireAccumulator = 0f;
    public float CooldownAccumulator = 0f;
    public float AcquireAccumulator = 0f;
    public float ParticleAccumulator = 0f;
    public EntityUid? FireSoundEntity = null;

    public EntityUid? GlowEntity = null;

    // Locked when firing starts; tracking follows this, not whoever is nearest now.
    public EntityUid? Target = null;
}
