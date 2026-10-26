using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSAilmentCureComponent : Component
{
    [DataField(required: true)]
    public FSAilmentSlot Slot;

    [DataField]
    public EntProtoId? Ward;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSteadyHandsComponent : Component
{
    [DataField]
    public float DelayMultiplier = 0.5f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSplintComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public TimeSpan SelfDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan? SplintedDuration;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/Medical/brutepack_end.ogg");

    [ViewVariables]
    public TimeSpan? BaseDelay;

    [ViewVariables]
    public TimeSpan? BaseSelfDelay;
}

[Serializable, NetSerializable]
public sealed partial class FSSplintDoAfterEvent : SimpleDoAfterEvent;
