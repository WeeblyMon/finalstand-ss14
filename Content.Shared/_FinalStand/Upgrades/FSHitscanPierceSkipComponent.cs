namespace Content.Shared._FinalStand.Upgrades;

[RegisterComponent]
public sealed partial class FSHitscanPierceSkipComponent : Component
{
    public readonly HashSet<EntityUid> Targets = new();
}
