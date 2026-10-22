namespace Content.Server._FinalStand.Grenades;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSGrenadeTimerBlinkSystem))]
public sealed partial class FSGrenadeTimerBlinkComponent : Component
{
    [AutoPausedField] public TimeSpan NextBlink;
    public bool ShowPrimed;
}
