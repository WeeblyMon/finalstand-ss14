using Robust.Shared.Audio;

namespace Content.Shared._FinalStand.Mech;

// A mech gun's reload or recharge sound, played at the mech a set time after a shot, or once the gun is ready again.
[RegisterComponent]
public sealed partial class FSMechCycleSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    // Seconds after each shot; leave unset to play when a real cooldown ends.
    [DataField]
    public float? Delay;

    public TimeSpan? PlayAt;
}
