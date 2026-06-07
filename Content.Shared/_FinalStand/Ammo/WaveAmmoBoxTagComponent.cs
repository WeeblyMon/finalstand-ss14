namespace Content.Shared._FinalStand.Ammo;

// client-visible marker so the indicator overlay can query ammo boxes without the server-only WaveAmmoBoxComponent
[RegisterComponent]
public sealed partial class WaveAmmoBoxTagComponent : Component { }
