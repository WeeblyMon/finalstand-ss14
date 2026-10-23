using System.Numerics;
using Content.Shared._FinalStand.Melee;

namespace Content.Client._FinalStand.Melee;

[RegisterComponent]
public sealed partial class FSMeleeShakeComponent : Component
{
    public float Trauma;
    public Vector2 Direction = Vector2.UnitY;
    public FSMeleeShakeMode Mode;
    public float Time;
}
