using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Repair;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSRivetGunComponent : Component
{
    [DataField]
    public ProtoId<StackPrototype> Material = "Steel";

    [DataField]
    public int RivetsPerSheet = 6;

    [AutoNetworkedField]
    public float FireRateMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float RepairCap = 0.75f;
}

[RegisterComponent]
public sealed partial class FSRivetComponent : Component
{
    [DataField]
    public float Heal = 15f;

    [DataField]
    public EntProtoId RepairEffect = "FSRivetSparks";

    [DataField]
    public SoundSpecifier RepairSound = new SoundPathSpecifier("/Audio/Effects/clang.ogg", AudioParams.Default.WithVolume(-4f).WithVariation(0.15f));
}
