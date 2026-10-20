using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Perks;

// Label is localized by the receiving client, with Percent as its { $value }.
[Serializable, NetSerializable]
public readonly record struct FSPerkTimedBuff(string PerkId, LocId Label, float Percent, TimeSpan EndTime);

/// <summary>
/// The timed perk buffs currently on the receiving player, shown as countdown rows on the wave HUD.
/// </summary>
[Serializable, NetSerializable]
public sealed class FSPerkTimedBuffsEvent(List<FSPerkTimedBuff> buffs) : EntityEventArgs
{
    public readonly List<FSPerkTimedBuff> Buffs = buffs;
}
