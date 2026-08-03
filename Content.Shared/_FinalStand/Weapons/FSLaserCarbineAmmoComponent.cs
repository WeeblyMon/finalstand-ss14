namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSLaserCarbineAmmoComponent : Component
{
    public int CurrentAmmo;
    public int MaxAmmo = 1;
}
