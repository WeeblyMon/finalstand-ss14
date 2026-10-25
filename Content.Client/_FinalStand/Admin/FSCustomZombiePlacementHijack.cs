using Robust.Client.Placement;
using Robust.Shared.Map;

namespace Content.Client._FinalStand.Admin;

public sealed class FSCustomZombiePlacementHijack : PlacementHijack
{
    private readonly FSCustomZombieEui _eui;

    public override bool CanRotate => false;

    public FSCustomZombiePlacementHijack(FSCustomZombieEui eui)
    {
        _eui = eui;
    }

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        _eui.RequestSpawn(coordinates);
        return true;
    }
}
