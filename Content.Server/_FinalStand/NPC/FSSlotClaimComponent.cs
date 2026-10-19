namespace Content.Server._FinalStand.NPC;

[RegisterComponent]
[Access(typeof(FSSlotRingSystem))]
public sealed partial class FSSlotClaimComponent : Component
{
    public EntityUid CCC;
    public int Ring;
    public int SlotIndex;
}
