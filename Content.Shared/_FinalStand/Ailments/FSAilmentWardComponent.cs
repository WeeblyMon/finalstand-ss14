namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent]
public sealed partial class FSAilmentWardComponent : Component
{
    [DataField(required: true)]
    public FSAilmentSlot Blocks;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Effect;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);
}
