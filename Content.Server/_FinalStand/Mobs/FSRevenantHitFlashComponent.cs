namespace Content.Server._FinalStand.Mobs;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSRevenantHitFlashComponent : Component
{
    [AutoPausedField] public TimeSpan LastFlash;
}
