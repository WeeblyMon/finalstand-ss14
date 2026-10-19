using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mech;

// A Giant-dropped overclock that reshapes one mech gun. One overclock per gun.
[RegisterComponent]
[Access(typeof(FSMechOverclockSystem))]
public sealed partial class FSMechOverclockComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Gun;

    [DataField(required: true)]
    public FSOverclockStats Stats = new();
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMechOverclockedComponent : Component
{
    [DataField, AutoNetworkedField]
    public FSOverclockStats Stats = new();
}

[DataDefinition, Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class FSOverclockStats
{
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public float FireRate = 1f;

    [DataField]
    public float Spread = 1f;

    [DataField]
    public int BurstBonus;

    [DataField]
    public float ProjectileSpeed = 1f;

    [DataField]
    public float Damage = 1f;
}
