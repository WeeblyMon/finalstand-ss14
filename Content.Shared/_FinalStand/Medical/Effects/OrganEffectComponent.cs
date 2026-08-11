// Components an organ grants to, or strips from, its body while attached.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.Medical.Effects;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentPause]
public sealed partial class OrganEffectComponent : Component
{
    [DataField]
    public ComponentRegistry? OnAdd;

    [DataField]
    public ComponentRegistry? OnRemove;

    [DataField]
    public ComponentRegistry Active = new();

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;
}
