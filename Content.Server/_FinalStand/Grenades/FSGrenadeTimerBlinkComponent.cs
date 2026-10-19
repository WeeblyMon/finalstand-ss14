namespace Content.Server._FinalStand.Grenades;

[RegisterComponent]
[Access(typeof(FSGrenadeTimerBlinkSystem))]
public sealed partial class FSGrenadeTimerBlinkComponent : Component
{
    public TimeSpan NextBlink;
    public bool ShowPrimed;
}
