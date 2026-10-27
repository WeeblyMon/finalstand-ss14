namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent]
public sealed partial class FSInjuryOnHitComponent : Component
{
    [DataField(required: true)]
    public FSInjurySpec Injury = new();
}
