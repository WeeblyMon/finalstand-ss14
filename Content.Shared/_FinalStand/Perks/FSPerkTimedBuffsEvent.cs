using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Perks;

[Serializable, NetSerializable]
public readonly record struct FSPerkTimedBuff(string PerkId, string Name, TimeSpan EndTime);

/// <summary>
/// The timed perk buffs currently on the receiving player, shown as countdown rows on the wave HUD.
/// </summary>
[Serializable, NetSerializable]
public sealed class FSPerkTimedBuffsEvent(List<FSPerkTimedBuff> buffs) : EntityEventArgs
{
    public readonly List<FSPerkTimedBuff> Buffs = buffs;
}
