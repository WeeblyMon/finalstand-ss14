using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._FinalStand.FriendlyFire;

/// <summary>
/// Broadcast when a player's melee swing hits at least one non-player, before damage is applied.
/// </summary>
public sealed class FSPlayerMeleeHitEvent(MeleeHitEvent hit) : EntityEventArgs
{
    public readonly MeleeHitEvent Hit = hit;
}
