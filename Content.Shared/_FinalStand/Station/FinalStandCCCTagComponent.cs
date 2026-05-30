namespace Content.Shared._FinalStand.Station;

// Client-visible marker so the CCC ready-up indicator overlay can query the
// CCC without the server-only FinalStandCCCComponent.
[RegisterComponent]
public sealed partial class FinalStandCCCTagComponent : Component { }
