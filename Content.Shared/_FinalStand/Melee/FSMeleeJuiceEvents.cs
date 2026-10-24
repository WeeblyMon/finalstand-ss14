using System.Numerics;
using Robust.Shared.Map;
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

// Sent by the attacker when they start or give up winding up a charged heavy attack.
[Serializable, NetSerializable]
public sealed class FSMeleeChargeEvent(NetEntity weapon, bool start) : EntityEventArgs
{
    public NetEntity Weapon = weapon;
    public bool Start = start;
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

// A knocked-back mob hitting a wall, for the squash and dust where it lands.
[Serializable, NetSerializable]
public sealed class FSMeleeWallBounceEvent(NetEntity target, Vector2 direction) : EntityEventArgs
{
    public NetEntity Target = target;
    public Vector2 Direction = direction;
}

// A burst of fire or force going off on its own, away from any swing.
[Serializable, NetSerializable]
public sealed class FSMeleeBurstEvent(NetCoordinates at, string effect) : EntityEventArgs
{
    public NetCoordinates At = at;
    public string Effect = effect;
}
