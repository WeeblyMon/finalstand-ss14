namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSMinigunComponent : Component
{
    public int CurrentAmmo;
    public int MaxAmmo = 1000;
}
