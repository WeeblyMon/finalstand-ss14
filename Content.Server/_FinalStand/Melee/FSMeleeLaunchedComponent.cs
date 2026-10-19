using System.Numerics;
using Content.Shared.Damage;

namespace Content.Server._FinalStand.Melee;

// A mob knocked flying by a launching hit, damaging whatever it crashes into until it lands.
[RegisterComponent]
public sealed partial class FSMeleeLaunchedComponent : Component
{
    public EntityUid User;
    public EntityUid Weapon;
    public DamageSpecifier Damage = new();
    public Vector2 Direction;
    public TimeSpan Until;
    public readonly HashSet<EntityUid> Hit = new();
}
