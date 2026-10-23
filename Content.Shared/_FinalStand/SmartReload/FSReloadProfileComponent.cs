using Robust.Shared.Audio;

namespace Content.Shared._FinalStand.SmartReload;

[RegisterComponent]
public sealed partial class FSReloadProfileComponent : Component
{
    [DataField]
    public float TimeMultiplier = 1f;

    [DataField]
    public SoundSpecifier? Sound;

    public EntityUid? Playing;
}
