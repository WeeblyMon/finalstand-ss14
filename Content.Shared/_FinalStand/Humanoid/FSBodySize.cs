namespace Content.Shared._FinalStand.Humanoid;

[DataDefinition]
public sealed partial class FSBodySize
{
    [DataField]
    public float MinHeight = 0.8f;

    [DataField]
    public float DefaultHeight = 1f;

    [DataField]
    public float MaxHeight = 1.2f;

    [DataField]
    public float MinWidth = 0.85f;

    [DataField]
    public float DefaultWidth = 1f;

    [DataField]
    public float MaxWidth = 1.15f;

    [DataField]
    public float SizeRatio = 1.2f;

    [DataField]
    public float AverageHeight = 176.1f;

    [DataField]
    public float AverageWidth = 40f;

    public float ClampHeight(float height)
    {
        return height <= 0f ? DefaultHeight : Math.Clamp(height, MinHeight, MaxHeight);
    }

    public float ClampWidth(float width)
    {
        return width <= 0f ? DefaultWidth : Math.Clamp(width, MinWidth, MaxWidth);
    }
}
