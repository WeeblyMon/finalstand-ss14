namespace Content.Shared._FinalStand.NPC;

[RegisterComponent]
public sealed partial class FSBreachCostOverrideComponent : Component
{
    /// <summary>
    /// Manual cost override. Set to 999f to make this structure untargetable by breach evaluator.
    /// Used for edge cases only — automatic HP-based cost handles all normal cases.
    /// </summary>
    [DataField]
    public float Cost = 1f;
}
