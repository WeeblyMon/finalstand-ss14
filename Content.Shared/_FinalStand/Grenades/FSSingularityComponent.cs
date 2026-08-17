using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Grenades;

[Serializable, NetSerializable]
public enum FSSingularityVisuals : byte
{
    Phase,
}

[Serializable, NetSerializable]
public enum FSSingularityPhase : byte
{
    Start,
    Loop,
    End,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSSingularityComponent : Component
{
    [DataField, AutoNetworkedField] public FSSingularityPhase Phase = FSSingularityPhase.Start;

    // Must match the length of the matching animation state.
    [DataField] public float StartDuration = 0.64f;

    [DataField] public float LoopDuration = 3f;

    // Must match the length of the matching animation state.
    [DataField] public float EndDuration = 0.48f;

    [DataField, AutoNetworkedField] public float Radius = 2.5f;

    // Radius the sprite covers at scale 1, used to scale the visual to match Radius.
    [DataField] public float VisualRadius = 1.8f;

    // Tiles per second at the rim, rising toward the centre.
    [DataField] public float PullStrength = 6f;

    [DataField] public DamageSpecifier DamagePerSecond = new()
    {
        DamageDict = { ["Blunt"] = 25f },
    };

    [DataField] public SoundSpecifier HumSound = new SoundPathSpecifier(
        "/Audio/_FinalStand/Effects/singularity_hum.ogg",
        AudioParams.Default.WithLoop(true).WithMaxDistance(14f));

    // Damage is credited to whoever threw the bomb, not to the singularity.
    public EntityUid? Thrower;

    public float Elapsed;
    public float DamageAccumulator;
    public EntityUid? HumStream;
}
