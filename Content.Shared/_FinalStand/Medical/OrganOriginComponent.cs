// The body an organ was grown in, so transplanting into a different one can be treated differently.

using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganOriginComponent : Component
{
    [DataField]
    public EntityUid? OriginalBody;
}
