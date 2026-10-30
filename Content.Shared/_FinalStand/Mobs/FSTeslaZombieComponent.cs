using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSTeslaZombieComponent : Component
{
    [DataField] public float DetectionRange = 7f;
    [DataField] public float ChainRange = 4f;
    [DataField] public float AttackCooldown = 5f;
    [DataField] public float FireDuration = 0.6f;
    [DataField] public int MaxChainTargets = 2;
    [DataField] public float PrimaryDamageShock = 15f;
    [DataField] public float ChainDamageShock = 9f;
    [DataField] public SoundSpecifier FireSound =
        new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg")
        {
            Params = AudioParams.Default.WithMaxDistance(12f).WithRolloffFactor(0.5f),
        };

    [AutoNetworkedField]
    public bool IsFiring = false;
    public EntityUid? Target = null;
    public float CooldownAccumulator = 0f;
    public float FireAccumulator = 0f;
}
