using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public sealed class FSDamageNumberEvent : EntityEventArgs
{
    public NetEntity Target;
    public float Amount;
    public bool IsCrit;
}
