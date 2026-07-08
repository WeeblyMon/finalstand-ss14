using Robust.Shared.Audio;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent]
public sealed partial class FSGiantStompComponent : Component
{
    [DataField] public float StompCooldown = 6f;
    [DataField] public float StompRadius = 6f;
    [DataField] public float ShakeRadius = 50f;

    [DataField] public float StompKnockbackMagnitude = 12f;

    [DataField]
    public SoundSpecifier? StompSound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");

    public float StompAccumulator = 0f;
}
