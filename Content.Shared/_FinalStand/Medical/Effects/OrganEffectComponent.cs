using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._FinalStand.Medical.Effects;

// Components an organ grants to, or strips from, its body while attached.
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentPause]
[Access(typeof(OrganEffectSystem), typeof(Content.Shared._Shitmed.Medical.Surgery.SharedSurgerySystem))]
public sealed partial class OrganEffectComponent : Component
{
    [DataField]
    public ComponentRegistry? OnAdd;

    [DataField]
    public ComponentRegistry? OnRemove;

    [DataField]
    public ComponentRegistry Active = new();

    // Granted components the body already had before this organ went in; they stay when it comes out.
    [ViewVariables]
    public HashSet<string> Preexisting = new();

    // OnRemove components this organ actually took off the body, so only those come back.
    [ViewVariables]
    public HashSet<string> Stripped = new();

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;
}
