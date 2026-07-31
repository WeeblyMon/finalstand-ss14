namespace Content.Shared._FinalStand.Research;

// Deliberately its own component rather than reusing vanilla ResearchDiskComponent - vanilla's
// ResearchDiskSystem feeds ResearchServerComponent.Points, not FSStationResearchComponent, and
// doesn't check args.Handled, so a disk with both components would double-process.
[RegisterComponent]
public sealed partial class FSResearchDiskComponent : Component
{
    [DataField]
    public int Points = 1000;
}
