using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Melee;

[Serializable, NetSerializable]
public sealed class FSMeleeHitJuiceEvent(NetEntity user, NetEntity weapon, List<NetEntity> targets, Vector2 direction, bool heavy, int strike, bool lethal, bool finisher) : EntityEventArgs
{
    public NetEntity User = user;
    public NetEntity Weapon = weapon;
    public List<NetEntity> Targets = targets;
    public Vector2 Direction = direction;
    public bool Heavy = heavy;

    public int Strike = strike;

    public bool Lethal = lethal;

    public bool Finisher = finisher;
}

[Serializable, NetSerializable]
public sealed class FSMeleeKillJuiceEvent(NetEntity user, NetEntity weapon, NetEntity target, bool heavy, bool finisher) : EntityEventArgs
{
    public NetEntity User = user;
    public NetEntity Weapon = weapon;
    public NetEntity Target = target;
    public bool Heavy = heavy;
    public bool Finisher = finisher;
}
