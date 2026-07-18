using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSMuzzleFlashColorComponent : Component
{
    [DataField(required: true)] public Color Color;
}
