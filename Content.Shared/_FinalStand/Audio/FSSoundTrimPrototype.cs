using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Audio;

/// <summary>
/// Per-file volume trims in dB, applied client-side wherever the file plays.
/// </summary>
[Prototype("fsSoundTrims")]
public sealed partial class FSSoundTrimPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<string, float> Trims = new();
}
