using Content.Shared._FinalStand.Titles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Onboarding;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSPlayerBadgeComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Rookie;

    [DataField, AutoNetworkedField]
    public ProtoId<FSTitlePrototype>? Title;
}
