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

    /// <summary>Must match the length of the matching animation state.</summary>
    [DataField] public float StartDuration = 0.64f;

    /// <summary>How long the pull holds before the collapse begins.</summary>
    [DataField] public float LoopDuration = 3f;

    /// <summary>Must match the length of the matching animation state.</summary>
    [DataField] public float EndDuration = 0.48f;

    [DataField, AutoNetworkedField] public float Radius = 2.5f;

    /// <summary>Radius the sprite covers at scale 1, used to scale the visual to match Radius.</summary>
    [DataField] public float VisualRadius = 1.8f;

    /// <summary>Pull speed in tiles per second at the rim, rising toward the centre.</summary>
    [DataField] public float PullStrength = 4f;

    /// <summary>Applied per second to everything caught in the pull.</summary>
    [DataField] public DamageSpecifier DamagePerSecond = new()
    {
        DamageDict = { ["Blunt"] = 25f },
    };

    [DataField] public SoundSpecifier HumSound = new SoundPathSpecifier(
        "/Audio/_FinalStand/Effects/singularity_hum.ogg",
        AudioParams.Default.WithLoop(true).WithMaxDistance(14f));

    /// <summary>Who threw the bomb. Damage is credited to them, not to the singularity.</summary>
    public EntityUid? Thrower;

    public float Elapsed;
    public float DamageAccumulator;
    public EntityUid? HumStream;
}
