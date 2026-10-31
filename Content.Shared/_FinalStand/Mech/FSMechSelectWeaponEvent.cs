using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mech;

[Serializable, NetSerializable]
public sealed class FSMechSelectWeaponEvent(NetEntity weapon) : EntityEventArgs
{
    public readonly NetEntity Weapon = weapon;
}
