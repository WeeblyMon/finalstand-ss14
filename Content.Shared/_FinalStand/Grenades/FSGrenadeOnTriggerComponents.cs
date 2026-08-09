using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Grenades;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSFireZoneOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField] public EntProtoId FireZoneProtoId = "FSFireZoneEffect";
    [DataField, AutoNetworkedField] public float BurnDuration = 5f;
    [DataField, AutoNetworkedField] public float EffectRadius = 0f;
    [DataField, AutoNetworkedField] public float DamageMultiplier = 1f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSStunInRadiusOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField, AutoNetworkedField] public float Radius = 5f;
    [DataField, AutoNetworkedField] public TimeSpan StunDuration = TimeSpan.FromSeconds(3);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSBaitOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField] public EntProtoId BaitProtoId = "FSBaitDecoy";
    [DataField, AutoNetworkedField] public float BaitDuration = 8f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSClusterOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField] public EntProtoId SubGrenadeProtoId = "FSGrenadeFragSub";
    [DataField, AutoNetworkedField] public int Count = 3;
    [DataField] public float Distance = 3f;
    [DataField] public float Velocity = 7f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSingularityOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField] public EntProtoId SingularityProtoId = "FSSingularityEffect";
    [DataField, AutoNetworkedField] public float ExtraRadius;
    [DataField, AutoNetworkedField] public float ExtraDuration;
    [DataField, AutoNetworkedField] public float DamageMultiplier = 1f;
}
