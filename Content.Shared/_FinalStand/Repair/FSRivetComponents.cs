using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Repair;

[RegisterComponent]
public sealed partial class FSRivetGunComponent : Component
{
    [DataField]
    public ProtoId<StackPrototype> Material = "Steel";

    [DataField]
    public int RivetsPerSheet = 6;
}

[RegisterComponent]
public sealed partial class FSRivetComponent : Component
{
    [DataField]
    public float Heal = 15f;

    [DataField]
    public float RepairCap = 0.75f;

    [DataField]
    public TimeSpan HitCooldown = TimeSpan.FromSeconds(1);
}
