using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Fortifications;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSUpgradeableComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<FSUpgradePrototype>> Options = new();

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<FSUpgradePrototype> Grade;

    [DataField, AutoNetworkedField]
    public ProtoId<FSUpgradePrototype>? Branch;

    [AutoNetworkedField]
    public ProtoId<FSUpgradePrototype>? Pending;

    [AutoNetworkedField]
    public int Poured;

    public float SteelHealth;

    public float WaveMultiplier = 1f;

    public TimeSpan? LastDamaged;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class FSEngineerHammerComponent : Component;

[Serializable, NetSerializable]
public enum FSUpgradeUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSUpgradeSelectMessage(ProtoId<FSUpgradePrototype> upgrade) : BoundUserInterfaceMessage
{
    public readonly ProtoId<FSUpgradePrototype> Upgrade = upgrade;
}
