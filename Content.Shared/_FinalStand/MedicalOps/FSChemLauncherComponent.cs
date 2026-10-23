using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSChemLauncherComponent : AmmoProviderComponent
{
    [ViewVariables] public float? BaseMaxReserve;
    [ViewVariables] public float? BaseHarvestRange;
    [ViewVariables] public float? BaseHarvestScale;
    [ViewVariables] public int? BaseSlotCount;

    [DataField, AutoNetworkedField]
    public List<ProtoId<FSChemFormulaPrototype>> Slots = new();

    [DataField, AutoNetworkedField]
    public int Selected;

    [DataField, AutoNetworkedField]
    public int SlotCount = 3;

    // Formulas this launcher can take, kept in step with research by the server.
    [DataField, AutoNetworkedField]
    public List<ProtoId<FSChemFormulaPrototype>> Known = new();

    [DataField, AutoNetworkedField]
    public float Reserve = 100f;

    [DataField, AutoNetworkedField]
    public float MaxReserve = 100f;

    [DataField]
    public float HarvestRange = 8f;

    // A kill refills killCredits / 30 reserve, times this.
    [DataField]
    public float HarvestScale = 1f;

    [DataField, AutoNetworkedField]
    public float Catalyst;

    [DataField, AutoNetworkedField]
    public bool CatalystArmed;
}

[RegisterComponent]
public sealed partial class FSChemCanisterComponent : Component
{
    [DataField(required: true)]
    public ProtoId<FSChemFormulaPrototype> Formula;

    [ViewVariables]
    public EntityUid? Launcher;

    [ViewVariables]
    public bool Catalysed;

    [ViewVariables]
    public bool Burst;
}

[Serializable, NetSerializable]
public sealed class FSChemLauncherSelectMessage(int slot) : EntityEventArgs
{
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class FSChemFormulaWheelEvent(NetEntity launcher) : EntityEventArgs
{
    public readonly NetEntity Launcher = launcher;
}
