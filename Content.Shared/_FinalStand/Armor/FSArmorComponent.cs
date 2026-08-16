using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Armor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSArmorComponent : Component
{
    // Both are derived from the mob's death threshold at startup, not authored in YAML.
    [AutoNetworkedField] public float MaxArmor;
    [AutoNetworkedField] public float CurrentArmor;

    [DataField] public float RegenRate = 10f;
    [DataField] public float MaxHPRatio = 0.5f;
    [DataField] public float RegenDelay = 5f;

    public float RegenDelayAccumulator;
    public float LastSyncedArmor;

    [DataField]
    public SoundSpecifier ArmorBreakSound = new SoundPathSpecifier("/Audio/_FinalStand/Armor/armor_break.ogg");
}
