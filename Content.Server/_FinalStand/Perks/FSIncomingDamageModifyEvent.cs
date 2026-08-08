// Raised after weapon resistances are applied, so perks modify incoming damage at the same
// point they always did without the upgrades module depending on the perks module.
using Content.Shared.Damage.Systems;

namespace Content.Server._FinalStand.Perks;

[ByRefEvent]
public readonly record struct FSIncomingDamageModifyEvent(EntityUid Target, DamageModifyEvent Args);
