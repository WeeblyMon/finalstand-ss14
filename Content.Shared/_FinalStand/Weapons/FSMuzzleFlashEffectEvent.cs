using System.Numerics;

namespace Content.Shared._FinalStand.Weapons;

// Raised as the client draws the muzzle flash, so mounted guns can move it to the barrel tip.
[ByRefEvent]
public record struct FSMuzzleFlashEffectEvent(Angle Angle)
{
    public Vector2 Offset;
}
