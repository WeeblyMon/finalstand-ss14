using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSEmergencyDefibComponent : Component
{
    [ViewVariables] public TimeSpan? BaseZapDelay;
    [ViewVariables] public float? BaseReviveHealthFraction;

    [DataField]
    public float ReviveHealthFraction = 0.2f;

    [DataField]
    public TimeSpan CardiovertDelay = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan CardiovertCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public SoundSpecifier CardiovertSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");
}

[Serializable, NetSerializable]
public sealed partial class FSCardiovertDoAfterEvent : SimpleDoAfterEvent;
