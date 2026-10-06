// Raised on a gun as the client draws its muzzle flash, so mounted guns can move it to the barrel tip and react to each shot.
using System.Numerics;

namespace Content.Shared._FinalStand.Weapons;

[ByRefEvent]
public record struct FSMuzzleFlashEffectEvent(Angle Angle)
{
    public Vector2 Offset;
}
