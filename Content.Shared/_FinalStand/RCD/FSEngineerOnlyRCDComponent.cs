namespace Content.Shared._FinalStand.RCD;

[RegisterComponent]
public sealed partial class FSEngineerOnlyRCDComponent : Component
{
    [DataField]
    public Dictionary<string, int> Caps = new();
}
