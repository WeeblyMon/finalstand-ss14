using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSArmouredDeflectComponent : Component
{
    [DataField] public float VulnerableDuration = 6f;
    [DataField] public float StanceDuration = 2f;
    [DataField] public float StartJitter = 4f;
    [DataField] public float ShrapnelRange = 3f;
    [DataField] public int ShrapnelCount = 6;

    [DataField] public float ShrapnelDamageFraction = 0.5f;

    [DataField] public string ShrapnelProto = "FSArmouredDeflectShard";

    [DataField]
    public SoundSpecifier DeflectSound =
        new SoundPathSpecifier("/Audio/_FinalStand/Mobs/Armoured/deflect.ogg")
        {
            Params = AudioParams.Default.WithMaxDistance(12f),
        };

    [AutoNetworkedField] public bool IsGlowing = false;

    public float PhaseTimer = -1f;
}
