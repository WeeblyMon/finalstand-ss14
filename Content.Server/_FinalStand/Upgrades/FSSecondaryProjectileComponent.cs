namespace Content.Server._FinalStand.Upgrades;

// Projectiles spawned by on-hit upgrades. They can't trigger on-hit spawns themselves, or they'd chain forever.
[RegisterComponent]
public sealed partial class FSSecondaryProjectileComponent : Component;
