using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._FinalStand.FriendlyFire;

// Broadcast when a player's melee swing hits at least one non-player, before damage is applied.
[ByRefEvent]
public readonly record struct FSPlayerMeleeHitEvent(MeleeHitEvent Hit);
