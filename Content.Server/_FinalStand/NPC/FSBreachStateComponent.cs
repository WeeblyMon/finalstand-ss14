using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

[RegisterComponent]
public sealed partial class FSBreachStateComponent : Component
{
    public readonly Dictionary<EntityUid, (int Count, TimeSpan FirstSelected)> SelectionHistory = new();

    public readonly Dictionary<EntityUid, TimeSpan> Blacklist = new();
}
