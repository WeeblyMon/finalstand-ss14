namespace Content.Server._FinalStand.Grenades;

// One quick throw per press: without it a burst of messages emptied a whole pack in a tick.
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(FSQuickGrenadeSystem))]
public sealed partial class FSQuickGrenadeCooldownComponent : Component
{
    [AutoPausedField]
    public TimeSpan NextThrow;
}
