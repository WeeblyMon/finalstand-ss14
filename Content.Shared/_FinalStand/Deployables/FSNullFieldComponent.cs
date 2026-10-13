namespace Content.Shared._FinalStand.Deployables;

// On the placed Null Field - see FSNullFieldSystem.
[RegisterComponent]
public sealed partial class FSNullFieldComponent : Component
{
    [DataField]
    public float Radius = 4f;

    [ViewVariables] public float? BaseRadius;

    [DataField]
    public float SlowFactor = 0.5f;
}
