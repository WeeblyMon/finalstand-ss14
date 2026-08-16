using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared._FinalStand.Grenades;

[Serializable, NetSerializable]
public enum GrenadeType : byte
{
    Frag,
    Incendiary,
    Flash,
    Pipe,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSGrenadePackComponent : Component
{
    [DataField, AutoNetworkedField] public GrenadeType PackType = GrenadeType.Frag;
    [DataField, AutoNetworkedField] public int Stock = 3;
    [DataField, AutoNetworkedField] public int MaxStock = 3;
    [DataField, AutoNetworkedField] public int RegenPerWave = 1;
    [DataField] public EntProtoId GrenadeProtoId = "FSGrenadeFrag";
    [DataField, AutoNetworkedField] public float BurnDuration = 5f;
    [DataField, AutoNetworkedField] public float StunDuration = 3f;
    [DataField, AutoNetworkedField] public float BaitDuration = 8f;
    [DataField, AutoNetworkedField] public bool ImpactFuse = false;
    [DataField, AutoNetworkedField] public float EffectRadius = 0f;
    [DataField, AutoNetworkedField] public float BlastBonus = 0f;
    [DataField, AutoNetworkedField] public bool IsCluster = false;
    [DataField, AutoNetworkedField] public bool IsSingularity = false;
    [DataField] public SpriteSpecifier? SingularitySprite;
    [DataField] public EntProtoId? SelectActionProtoId;
    [DataField] public EntityUid? GrantedActionId;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSActiveGrenadeComponent : Component
{
    [DataField, AutoNetworkedField] public GrenadeType ActiveType = GrenadeType.Frag;
}

[RegisterComponent]
public sealed partial class FSGrenadeSelectActionComponent : Component
{
    [DataField] public GrenadeType GrenadeType = GrenadeType.Frag;
}

public sealed partial class FSSelectFragGrenadeEvent : InstantActionEvent { }
public sealed partial class FSSelectIncendiaryGrenadeEvent : InstantActionEvent { }
public sealed partial class FSSelectFlashGrenadeEvent : InstantActionEvent { }
public sealed partial class FSSelectPipeGrenadeEvent : InstantActionEvent { }

[RegisterComponent]
public sealed partial class FSBaitDecoyComponent : Component { }
