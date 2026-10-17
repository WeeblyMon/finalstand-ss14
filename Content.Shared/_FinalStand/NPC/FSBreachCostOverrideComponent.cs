namespace Content.Shared._FinalStand.NPC;

[RegisterComponent]
public sealed partial class FSBreachCostOverrideComponent : Component
{
    /// <summary>
    /// Set to 999 to make the breach evaluator ignore this structure.
    /// Edge cases only; the automatic HP-based cost covers normal ones.
    /// </summary>
    [DataField]
    public float Cost = 1f;
}
