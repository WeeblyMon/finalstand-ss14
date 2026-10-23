namespace Content.Server._FinalStand.Melee;

// On a gun while a melee weapon fires it, so the gun's own muzzle flash doesn't go off inside a pack.
[RegisterComponent]
public sealed partial class FSMeleeQuietShotComponent : Component;
