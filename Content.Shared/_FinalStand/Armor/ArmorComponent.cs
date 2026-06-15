using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Armor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSArmorComponent : Component
{
    [DataField] public float MaxArmor = 0f;
    [DataField] public float CurrentArmor = 0f;
    [DataField] public float RegenRate = 10f;
    [DataField] public float MaxHPRatio = 0.5f;
    [DataField] public float RegenDelay = 5f;

    public float RegenDelayAccumulator = 0f;

    [DataField, AutoNetworkedField] public float NetworkedCurrentArmor = 0f;
    [DataField, AutoNetworkedField] public float NetworkedMaxArmor = 0f;

    [DataField]
    public SoundSpecifier ArmorBreakSound = new SoundPathSpecifier("/Audio/_FinalStand/Armor/armor_break.ogg");
}
