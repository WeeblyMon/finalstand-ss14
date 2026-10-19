using Content.Shared._Shitmed.Body;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.Medical;

// Simple bodies (borgs, animals) don't carry this. Healing only runs on the server, so nothing here is networked.
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BodyWoundHealingComponent : Component
{
    [DataField]
    public BodyType BodyType = BodyType.Complex;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan HealAt;
}
