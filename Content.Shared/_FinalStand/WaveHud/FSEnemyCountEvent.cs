using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public sealed class FSEnemyCountEvent : EntityEventArgs
{
    public readonly int Alive;
    public readonly int Total;
    public FSEnemyCountEvent(int alive, int total) { Alive = alive; Total = total; }
}
