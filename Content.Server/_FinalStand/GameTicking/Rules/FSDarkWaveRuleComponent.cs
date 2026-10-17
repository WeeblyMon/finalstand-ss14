using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._FinalStand.GameTicking.Rules;

// Dark waves: the station blacks out, doors lose power, radios jam and revenants hunt the crew for a fixed time.
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(FSDarkWaveSystem))]
public sealed partial class FSDarkWaveRuleComponent : Component
{
    [DataField]
    public int GuaranteedWave = 8;

    // Earliest wave a random roll can pick.
    [DataField]
    public int FirstRandomWave = 6;

    [DataField]
    public float Chance = 0.05f;

    [DataField]
    public int CooldownWaves = 5;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(120);

    // How far into prep the warning goes out.
    [DataField]
    public TimeSpan WarningDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan SpawnInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public int MaxAlive = 3;

    [DataField]
    public float FlickerFraction = 0.85f;

    [DataField]
    public TimeSpan FlickerOffTime = TimeSpan.FromSeconds(0.22);

    [DataField]
    public TimeSpan FlickerIntervalMin = TimeSpan.FromSeconds(0.7);

    [DataField]
    public TimeSpan FlickerIntervalMax = TimeSpan.FromSeconds(2.2);

    [DataField]
    public SoundSpecifier? WarningSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/darkwave_warning.ogg");

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/darkwave_start.ogg");

    [DataField]
    public SoundSpecifier? AmbienceSound = new SoundPathSpecifier("/Audio/_FinalStand/WaveEvents/darkwave_ambience.ogg");

    [ViewVariables]
    public bool Upcoming;

    [ViewVariables]
    public bool Active;

    // Set by forcedarkwave mid-combat, picked up at the next prep.
    [ViewVariables]
    public bool Forced;

    [ViewVariables]
    public int WavesSinceLast = 999;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? WarningAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFlicker;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? FlickerRestoreAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextBlackoutCheck;

    [ViewVariables]
    public EntityUid? AmbienceStream;

    [ViewVariables]
    public readonly List<EntityUid> FlickeredLights = new();

    [ViewVariables]
    public readonly List<EntityUid> DepoweredDoors = new();

    [ViewVariables]
    public readonly List<EntityUid> CutApcs = new();
}
