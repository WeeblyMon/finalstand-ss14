using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Ailments;

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
