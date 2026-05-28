namespace Content.Server._FinalStand.Upgrades;

// per-target hit counter for Resonance; ignites at HitsToIgnite and resets entry
[RegisterComponent]
public sealed partial class FSResonanceComponent : Component
{
    public Dictionary<EntityUid, int> HitCounts = new();
    public const int HitsToIgnite = 5;
}
