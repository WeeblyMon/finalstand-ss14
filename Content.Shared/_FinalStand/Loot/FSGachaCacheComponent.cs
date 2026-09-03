using Content.Shared.Random;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Loot;

// maintenance loot cache: crack it open by hacking (loud) or by paying (quiet)
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSGachaCacheComponent : Component
{
    [DataField, AutoNetworkedField]
    public int UsesLeft = 1;

    [DataField]
    public int Price = 3500;

    [DataField]
    public TimeSpan HackDuration = TimeSpan.FromSeconds(15);

    [DataField]
    public ProtoId<WeightedRandomEntityPrototype> CommonPool = "FSGachaImprovised";

    [DataField]
    public ProtoId<WeightedRandomEntityPrototype> UncommonPool = "FSGachaSurplus";

    [DataField]
    public ProtoId<WeightedRandomEntityPrototype> RarePool = "FSGachaJackpot";

    [DataField]
    public ProtoId<WeightedRandomEntityPrototype> MiscPool = "FSGachaMisc";

    [DataField]
    public float UncommonChance = 0.20f;

    [DataField]
    public float RareChance = 0.05f;

    [DataField]
    public float MiscChance = 0.6f;
}

[Serializable, NetSerializable]
public enum FSGachaCacheVisuals : byte
{
    Looted,
}
