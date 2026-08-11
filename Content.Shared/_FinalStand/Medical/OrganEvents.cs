// Organ enable/disable. Limbs are organs here, so this replaces Goob's split part and organ pairs.

using Content.Shared.Body;

namespace Content.Shared._FinalStand.Medical;

[ByRefEvent]
public readonly record struct OrganEnableChangedEvent(bool Enabled);

[ByRefEvent]
public readonly record struct OrganEnabledEvent(Entity<OrganComponent> Organ);

[ByRefEvent]
public readonly record struct OrganDisabledEvent(Entity<OrganComponent> Organ);

[ByRefEvent]
public record struct TryRemoveOrganEvent(EntityUid OrganId, OrganComponent? Organ = null, bool Cancelled = false);
