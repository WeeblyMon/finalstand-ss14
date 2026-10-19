using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.Weapons;

[RegisterComponent]
[Access(typeof(FSBeamFollowSystem))]
public sealed partial class FSBeamFollowComponent : Component
{
    public EntityUid Shooter;

    public EntityCoordinates Impact;

    public SharedGunSystem.HitscanPart Part;
}
