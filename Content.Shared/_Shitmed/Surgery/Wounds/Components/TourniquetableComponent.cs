using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TourniquetableComponent : Component
{
    public EntityUid? CurrentTourniquetEntity;

    [AutoNetworkedField]
    public ProtoId<OrganCategoryPrototype> SeveredCategory = "Head";
}