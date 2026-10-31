using Content.Shared.Damage.Systems;

namespace Content.Server._FinalStand.Perks;

// Raised after weapon resistances, so perks hook in there without the upgrades module depending on perks.
[ByRefEvent]
public readonly record struct FSIncomingDamageModifyEvent(EntityUid Target, DamageModifyEvent Args);
