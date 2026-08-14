// Lets a limb or organ be held as the "tool" for the surgery step that implants it.

using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganSurgeryToolComponent : Component, ISurgeryToolComponent
{
    [DataField]
    public string ToolName { get; set; } = "an organ";

    [DataField]
    public bool? Used { get; set; }

    [DataField]
    public float Speed { get; set; } = 1f;
}
