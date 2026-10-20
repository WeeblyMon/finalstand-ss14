using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Actions;

// Actions handed to a mob for its job or perks, by prototype, so each is granted once and goes away with the mob.
[RegisterComponent, Access(typeof(FSGrantedActionsSystem))]
public sealed partial class FSGrantedActionsComponent : Component
{
    [ViewVariables]
    public readonly Dictionary<EntProtoId, EntityUid> Actions = new();
}
