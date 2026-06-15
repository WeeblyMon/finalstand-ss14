using Content.Shared._Shitmed.Body;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Body.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
// [Access(typeof(SharedBodySystem))] // Goob edit - removed for _Shitmed access
public sealed partial class BodyComponent : Component
{
    public const string ContainerID = SharedBodySystem.BodyRootContainerId;

    [DataField]
    public ProtoId<BodyPrototype>? Prototype;

    [ViewVariables]
    public ContainerSlot RootContainer = default!;

    [DataField, AutoNetworkedField]
    public BodyType BodyType = BodyType.Complex;

    [DataField]
    public int RequiredLegs = 2;

    [ViewVariables]
    public List<EntityUid> LegEntities = new();

    [DataField]
    public TimeSpan HealAt = TimeSpan.Zero;
}
