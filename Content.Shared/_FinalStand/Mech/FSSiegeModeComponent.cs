using Content.Shared.Actions;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mech;

// Planted, the mech can't move, wears heavier armour and shrugs off hits from the front.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(FSSiegeModeSystem))]
public sealed partial class FSSiegeModeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Planted;

    [DataField]
    public EntProtoId Action = "FSActionMechSiege";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public ProtoId<DamageModifierSetPrototype> SiegeArmor = "FSMechArmorSiege";

    [DataField]
    public ProtoId<DamageModifierSetPrototype> NormalArmor = "FSMechArmor";

    [DataField]
    public float FrontalArc = 120f;

    [DataField]
    public float FrontalDamage = 0.5f;

    [DataField]
    public TimeSpan PlantTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan UnplantTime = TimeSpan.FromSeconds(2);
}

public sealed partial class FSSiegeModeActionEvent : InstantActionEvent;

[ByRefEvent]
public readonly record struct FSSiegePlantedEvent(bool Planted);

[Serializable, NetSerializable]
public sealed partial class FSSiegeModeDoAfterEvent : SimpleDoAfterEvent;
