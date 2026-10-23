using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.Loot;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class FSMysteryBoxComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Price = 75000;

    [DataField(required: true)]
    public List<FSMysteryBoxTier> Tiers = [];

    [DataField]
    public TimeSpan SpinTime = TimeSpan.FromSeconds(4.2);

    [DataField]
    public TimeSpan OfferTime = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan CloseTime = TimeSpan.FromSeconds(0.35);

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Effects/pop.ogg");

    [DataField]
    public SoundSpecifier SpinSound = new SoundPathSpecifier("/Audio/Machines/spinning.ogg");

    [DataField]
    public SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/Arcade/win.ogg");

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Items/toolbox_close.ogg");

    [DataField, AutoNetworkedField]
    public FSMysteryBoxState State;

    [DataField, AutoNetworkedField]
    public EntProtoId? Prize;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StateStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StateEnd;

    [DataField]
    public EntityUid? Buyer;

    [DataField]
    public EntProtoId? PendingPrize;

    [ViewVariables]
    public TimeSpan NextIcon;
}

[DataDefinition]
public sealed partial class FSMysteryBoxTier
{
    [DataField(required: true)]
    public ProtoId<WeightedRandomEntityPrototype> Pool;

    [DataField]
    public float Weight = 1f;
}

[Serializable, NetSerializable]
public enum FSMysteryBoxState : byte
{
    Idle,
    Spinning,
    Offering,
    Closing,
}
