using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FSSkippedBeatComponent : Component
{
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float SpeedScale = 0.5f;

    [DataField]
    public EntProtoId Slowdown = "FSSkippedBeatStatusEffect";

    [DataField]
    public EntProtoId? Spark;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextBeat;
}
