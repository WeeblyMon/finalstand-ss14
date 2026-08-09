namespace Content.Server._FinalStand.Grenades;

[RegisterComponent]
public sealed partial class FSGrenadeTimerBlinkComponent : Component
{
    public TimeSpan NextBlink;
    public bool ShowPrimed;
}
