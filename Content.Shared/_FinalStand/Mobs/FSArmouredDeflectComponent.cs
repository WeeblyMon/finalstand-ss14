using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSArmouredDeflectComponent : Component
{
    [DataField] public float DeflectChance = 0.06f;

    [DataField]
    public SoundSpecifier DeflectSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Mobs/Armoured/deflect.ogg")
        {
            Params = AudioParams.Default.WithMaxDistance(12f),
        };

    [AutoNetworkedField] public bool IsGlowing = false;
    public float GlowTimer = 0f;
    public const float GlowDuration = 0.4f;
}
