namespace Content.Shared._FinalStand.Visuals;

// Folding barricades swap their cracks overlay to this RSI while folded down.
[RegisterComponent]
public sealed partial class FSFoldedDamageOverlayComponent : Component
{
    [DataField(required: true)]
    public string Folded = string.Empty;

    public string? Upright;
}
