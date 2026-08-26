namespace Content.Server._FinalStand.Structures;

// Only wave-spawned enemies can damage this; everything else is ignored.
[RegisterComponent]
public sealed partial class FSWaveDamageOnlyComponent : Component;
