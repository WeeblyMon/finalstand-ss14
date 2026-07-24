using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public sealed class FSDamageNumberEvent : EntityEventArgs
{
    public NetEntity Target;
    public float Amount;
    public bool IsCrit;
}

[Serializable, NetSerializable]
public sealed class FSArmorDamageNumberEvent : EntityEventArgs
{
    public NetEntity Target;
    public float Amount;
}

[Serializable, NetSerializable]
public sealed class FSLevelUpNumberEvent : EntityEventArgs
{
    public NetEntity Target;
    public int ApGained;
}

[Serializable, NetSerializable]
public sealed class FSHealNumberEvent : EntityEventArgs
{
    public NetEntity Target;
    public float Amount;
}
