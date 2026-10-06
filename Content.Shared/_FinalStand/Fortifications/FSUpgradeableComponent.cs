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

    [DataField, AutoNetworkedField]
    public List<ProtoId<FSUpgradePrototype>> Modules = new();

    [AutoNetworkedField]
    public ProtoId<FSUpgradePrototype>? Pending;

    [AutoNetworkedField]
    public int Poured;

    public float SteelHealth;

    public float WaveMultiplier = 1f;

    public TimeSpan? LastDamaged;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSEngineerHammerComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<string, int> SiloStock = new();
}

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
